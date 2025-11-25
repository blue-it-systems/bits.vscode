import * as vscode from 'vscode';
import * as path from 'path';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient | undefined;
let statusBarItem: vscode.StatusBarItem;
let outputChannel: vscode.OutputChannel;

// State Icons
const STATE_ICONS: Record<string, string> = {
    'Idle': '$(circle-slash)',
    'GeneratorFound': '$(search)',
    'Compiling': '$(sync~spin)',
    'Ready': '$(check)',
    'Running': '$(play)',
    'Error': '$(error)',
    'Stopped': '$(debug-stop)'
};

// State Colors
const STATE_COLORS: Record<string, vscode.ThemeColor | undefined> = {
    'Idle': undefined,
    'GeneratorFound': new vscode.ThemeColor('statusBarItem.warningForeground'),
    'Compiling': new vscode.ThemeColor('statusBarItem.warningForeground'),
    'Ready': new vscode.ThemeColor('statusBarItem.prominentForeground'),
    'Running': new vscode.ThemeColor('statusBarItem.prominentForeground'),
    'Error': new vscode.ThemeColor('statusBarItem.errorForeground'),
    'Stopped': undefined
};

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    outputChannel = vscode.window.createOutputChannel('Gengora');
    context.subscriptions.push(outputChannel);

    // Create Status Bar Item
    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    statusBarItem.command = 'gengora.showOutput';
    context.subscriptions.push(statusBarItem);

    updateStatusBar('Idle');

    // Register Commands
    context.subscriptions.push(
        vscode.commands.registerCommand('gengora.recompile', recompileCommand),
        vscode.commands.registerCommand('gengora.stop', stopCommand),
        vscode.commands.registerCommand('gengora.showOutput', () => outputChannel.show())
    );

    // Check If Auto-Start Is Enabled
    const config = vscode.workspace.getConfiguration('gengora');
    const autoStart = config.get<boolean>('autoStart', true);

    if (autoStart) {
        await startLanguageServer(context);
    }

    outputChannel.appendLine('Gengora Extension Activated');
}

export function deactivate(): Thenable<void> | undefined {
    if (!client) {
        return undefined;
    }

    return client.stop();
}

async function startLanguageServer(context: vscode.ExtensionContext): Promise<void> {
    const config = vscode.workspace.getConfiguration('gengora');

    // Determine Server Path
    let serverPath = config.get<string>('serverPath', '');

    if (!serverPath) {
        // Use Bundled Server
        serverPath = context.asAbsolutePath(
            path.join('..', 'server', 'Gengora.Server', 'bin', 'Debug', 'net10.0', 'Gengora.Server.dll')
        );
    }

    outputChannel.appendLine(`Server Path: ${serverPath}`);

    // Server Options
    const serverOptions: ServerOptions = {
        run: {
            command: 'dotnet',
            args: [serverPath],
            transport: TransportKind.stdio
        },
        debug: {
            command: 'dotnet',
            args: [serverPath],
            transport: TransportKind.stdio
        }
    };

    // Client Options
    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'csharp' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.cs')
        },
        outputChannel: outputChannel,
        initializationOptions: {
            capabilities: {
                statusBar: true,
                diagnostics: true
            }
        }
    };

    // Create And Start Client
    client = new LanguageClient(
        'gengora',
        'Gengora Language Server',
        serverOptions,
        clientOptions
    );

    // Handle Notifications
    client.onNotification('gengora/stateChanged', handleStateChanged);
    client.onNotification('gengora/diagnostics', handleDiagnostics);
    client.onNotification('gengora/fileEmitted', handleFileEmitted);

    // Start Client
    await client.start();

    outputChannel.appendLine('Language Server Started');
}

function updateStatusBar(state: string, message?: string): void {
    const icon = STATE_ICONS[state] ?? '$(question)';
    const displayText = message ?? state;

    statusBarItem.text = `${icon} Gengora: ${displayText}`;
    statusBarItem.color = STATE_COLORS[state];
    statusBarItem.tooltip = `Gengora Generator State: ${state}${message ? `\n${message}` : ''}`;
    statusBarItem.show();
}

function handleStateChanged(notification: StateChangedNotification): void {
    outputChannel.appendLine(`State Changed: ${notification.previousState} → ${notification.state}`);

    if (notification.message) {
        outputChannel.appendLine(`  Message: ${notification.message}`);
    }

    updateStatusBar(notification.state, notification.message);

    // Show Error Notification
    if (notification.state === 'Error' && notification.message) {
        vscode.window.showErrorMessage(`Gengora: ${notification.message}`);
    }
}

function handleDiagnostics(notification: DiagnosticsNotification): void {
    outputChannel.appendLine(`Received ${notification.diagnostics.length} Diagnostic(s)`);

    for (const diagnostic of notification.diagnostics) {
        const location = diagnostic.filePath
            ? `${diagnostic.filePath}:${diagnostic.line}:${diagnostic.column}`
            : '(unknown)';

        outputChannel.appendLine(`  [${diagnostic.severity}] ${diagnostic.id}: ${diagnostic.message} at ${location}`);
    }

    if (notification.isCompilationError) {
        vscode.window.showErrorMessage(
            `Gengora: Compilation Failed With ${notification.diagnostics.length} Error(s)`,
            'Show Output'
        ).then(selection => {
            if (selection === 'Show Output') {
                outputChannel.show();
            }
        });
    }
}

function handleFileEmitted(notification: FileEmittedNotification): void {
    outputChannel.appendLine(`File Emitted: ${notification.path}`);
}

async function recompileCommand(): Promise<void> {
    if (!client) {
        vscode.window.showWarningMessage('Gengora: Language Server Not Running');
        return;
    }

    try {
        const result = await client.sendRequest<RecompileResult>('gengora/recompile');

        if (result.success) {
            vscode.window.showInformationMessage('Gengora: Recompilation Succeeded');
        } else {
            vscode.window.showErrorMessage(`Gengora: ${result.message ?? 'Recompilation Failed'}`);
        }
    } catch (error) {
        vscode.window.showErrorMessage(`Gengora: ${error}`);
    }
}

async function stopCommand(): Promise<void> {
    if (!client) {
        vscode.window.showWarningMessage('Gengora: Language Server Not Running');
        return;
    }

    try {
        await client.sendRequest('gengora/stop');
        vscode.window.showInformationMessage('Gengora: Generator Stopped');
    } catch (error) {
        vscode.window.showErrorMessage(`Gengora: ${error}`);
    }
}

// Type Definitions For Notifications
interface StateChangedNotification {
    state: string;
    previousState: string;
    message?: string;
    timestamp: string;
}

interface DiagnosticsNotification {
    diagnostics: LspDiagnostic[];
    isCompilationError: boolean;
}

interface LspDiagnostic {
    id: string;
    message: string;
    severity: string;
    filePath?: string;
    line?: number;
    column?: number;
}

interface FileEmittedNotification {
    path: string;
    timestamp: string;
}

interface RecompileResult {
    success: boolean;
    message?: string;
}
