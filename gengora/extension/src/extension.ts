import * as path from 'path';
import * as fs from 'fs';
import * as vscode from 'vscode';
import { LanguageClient, TransportKind } from 'vscode-languageclient/node';
import * as Constants from './constants';

let client: LanguageClient | undefined;
let output: vscode.OutputChannel;
let statusBar: vscode.StatusBarItem | undefined;

// Log levels: error = 0, warning = 1, info = 2, debug = 3
enum LogLevel {
    Error = 0,
    Warning = 1,
    Info = 2,
    Debug = 3
}

let currentLogLevel: LogLevel = LogLevel.Warning;

function log(level: LogLevel, message: string) {
    if (level <= currentLogLevel) {
        const prefix = ['[ERROR]', '[WARN]', '[INFO]', '[DEBUG]'][level];
        output.appendLine(`${prefix} ${message}`);
    }
}

// Create a custom output channel that respects log levels
class FilteredOutputChannel implements vscode.OutputChannel {
    readonly name: string;
    private channel: vscode.OutputChannel;

    constructor(name: string, channel: vscode.OutputChannel) {
        this.name = name;
        this.channel = channel;
    }

    append(value: string): void {
        // LSP client messages go through as debug level
        log(LogLevel.Debug, value.trim());
    }

    appendLine(value: string): void {
        log(LogLevel.Debug, value);
    }

    replace(value: string): void {
        this.channel.replace(value);
    }

    clear(): void {
        this.channel.clear();
    }

    show(preserveFocus?: boolean): void;
    show(column?: vscode.ViewColumn, preserveFocus?: boolean): void;
    show(columnOrPreserveFocus?: vscode.ViewColumn | boolean, preserveFocus?: boolean): void {
        this.channel.show(columnOrPreserveFocus as any, preserveFocus);
    }

    hide(): void {
        this.channel.hide();
    }

    dispose(): void {
        this.channel.dispose();
    }
}

// No longer needed - server handles all discovery and file watching

export async function activate(context: vscode.ExtensionContext) {
    try {
        output = vscode.window.createOutputChannel('Gengora');
        output.show(true);
        context.subscriptions.push(output);

        // Load configuration
        const config = vscode.workspace.getConfiguration('gengora');
        const logLevelStr = config.get<string>('logLevel') || Constants.Defaults.LOG_LEVEL;
        currentLogLevel = { error: LogLevel.Error, warning: LogLevel.Warning, info: LogLevel.Info, debug: LogLevel.Debug }[logLevelStr] ?? LogLevel.Warning;

        log(LogLevel.Info, '=== Gengora Extension Activating ===');
        log(LogLevel.Debug, `Extension path: ${context.extensionPath}`);
        log(LogLevel.Debug, `Log level: ${logLevelStr}`);

        // Check if user manually stopped the server in a previous session
        const isManuallyStopped = context.workspaceState.get<boolean>('gengora.manuallyStopped', false);
        log(LogLevel.Debug, `Manual stop state: ${isManuallyStopped}`);

        // Status bar
        statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
        statusBar.text = Constants.StatusBar.INITIALIZING;
        statusBar.tooltip = Constants.StatusBar.TOOLTIP;
        statusBar.command = Constants.Commands.GENGORA_SHOW_OUTPUT;
        statusBar.show();
        context.subscriptions.push(statusBar);

        // Find workspace root
        const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
        if (!workspaceRoot) {
            const msg = 'No workspace folder opened. Please open a folder containing your generator project.';
            log(LogLevel.Error, msg);
            vscode.window.showErrorMessage(msg);
            statusBar.text = Constants.StatusBar.NO_WORKSPACE;
            return;
        }

        log(LogLevel.Debug, `Workspace root: ${workspaceRoot}`);

        // Get user-specified project path (if any)
        const configuredProjectPath = config.get<string>('generatorProjectPath') || '';
        
        if (configuredProjectPath) {
            log(LogLevel.Info, `Using configured generator project path: ${configuredProjectPath}`);
        } else {
            log(LogLevel.Info, 'Auto-discovering generator project via marker...');
        }

        // Find server DLL - check bundled location first, then dev location
        let serverPath = config.get<string>('serverPath') || '';
        if (!serverPath) {
            // Try bundled location (for published extension)
            const bundledPath = path.join(context.extensionPath, 'bin', Constants.Build.TARGET_FRAMEWORK, Constants.Build.SERVER_DLL_NAME);
            // Try dev location (for development)
            const devPath = path.join(context.extensionPath, '..', 'server', 'bin', Constants.Build.RELEASE_CONFIG, Constants.Build.TARGET_FRAMEWORK, Constants.Build.SERVER_DLL_NAME);
            const debugPath = path.join(context.extensionPath, '..', 'server', 'bin', Constants.Build.DEBUG_CONFIG, Constants.Build.TARGET_FRAMEWORK, Constants.Build.SERVER_DLL_NAME);
            
            if (fs.existsSync(bundledPath)) {
                serverPath = bundledPath;
                log(LogLevel.Debug, 'Using bundled server');
            } else if (fs.existsSync(devPath)) {
                serverPath = devPath;
                log(LogLevel.Debug, 'Using dev (Release) server');
            } else if (fs.existsSync(debugPath)) {
                serverPath = debugPath;
                log(LogLevel.Debug, 'Using dev (Debug) server');
            } else {
                const msg = `Server not found. Tried:\n- ${bundledPath}\n- ${devPath}\n- ${debugPath}`;
                log(LogLevel.Error, msg);
                vscode.window.showErrorMessage('Gengora: Server DLL not found. Please reinstall the extension.');
                statusBar.text = Constants.StatusBar.SERVER_MISSING;
                return;
            }
        }

        if (!fs.existsSync(serverPath)) {
            const msg = `Server not found at configured path: ${serverPath}`;
            log(LogLevel.Error, msg);
            vscode.window.showErrorMessage(msg);
            statusBar.text = Constants.StatusBar.SERVER_MISSING;
            return;
        }

        log(LogLevel.Info, `Server: ${serverPath}`);

        // Start language server with environment variables for configuration
        const isDll = serverPath.toLowerCase().endsWith('.dll');
        const serverEnv = {
            ...process.env,
            ...(configuredProjectPath && { GENERATOR_PROJECT_PATH: configuredProjectPath })
        };
        
        const serverOptions = isDll 
            ? { 
                command: 'dotnet', 
                args: [serverPath, Constants.CliArgs.WORKSPACE_ROOT, workspaceRoot], 
                transport: TransportKind.stdio,
                options: { env: serverEnv }
            }
            : { 
                command: serverPath, 
                args: [Constants.CliArgs.WORKSPACE_ROOT, workspaceRoot], 
                transport: TransportKind.stdio,
                options: { env: serverEnv }
            };

        const clientOptions = {
            documentSelector: [{ scheme: 'file' }],
            outputChannel: new FilteredOutputChannel('Gengora LSP', output)
        };

        client = new LanguageClient('gengora', 'Gengora LSP', serverOptions, clientOptions);

        log(LogLevel.Debug, 'Starting language client...');
        await client.start();
        log(LogLevel.Info, 'Language client started');

        // Auto-start server unless user manually stopped it previously
        if (!isManuallyStopped) {
            log(LogLevel.Info, 'Auto-starting generator (not manually stopped)...');
            try {
                await new Promise(resolve => setTimeout(resolve, 500)); // Brief delay for server initialization
                if (client && client.isRunning()) {
                    await client.sendRequest(Constants.Methods.WORKSPACE_EXECUTE_COMMAND, { command: Constants.Commands.GENGORA_START });
                }
            } catch (error: any) {
                log(LogLevel.Warning, `Auto-start failed: ${error?.message ?? error}`);
            }
        } else {
            log(LogLevel.Info, 'Skipping auto-start (server was manually stopped in previous session)');
        }

        // Register notification handlers
        client.onNotification(Constants.Notifications.GENERATOR_STDOUT, (params: any) => {
            log(LogLevel.Debug, `[Gengora] ${params.text ?? String(params)}`);
        });

        client.onNotification(Constants.Notifications.GENERATOR_STDERR, (params: any) => {
            log(LogLevel.Warning, `[Gengora stderr] ${params.text ?? String(params)}`);
        });

        client.onNotification(Constants.Notifications.GENERATOR_STATUS, (params: any) => {
            const state = params?.state ?? '';
            const message = params?.message ?? '';
            
            if (statusBar) {
                switch (state) {
                    case Constants.States.COMPILING:
                        log(LogLevel.Debug, '[Gengora] Compiling generator...');
                        statusBar.text = Constants.StatusBar.COMPILING;
                        break;
                    case Constants.States.COMPILED:
                        log(LogLevel.Debug, '[Gengora] Generator compiled successfully');
                        statusBar.text = Constants.StatusBar.COMPILED;
                        break;
                    case Constants.States.RUNNING:
                        log(LogLevel.Debug, '[Gengora] Generator starting up...');
                        statusBar.text = Constants.StatusBar.RUNNING;
                        break;
                    case Constants.States.ERROR:
                        log(LogLevel.Error, `[Gengora] Error: ${message}`);
                        statusBar.text = Constants.StatusBar.ERROR;
                        break;
                    case Constants.States.STOPPED:
                        log(LogLevel.Debug, '[Gengora] Generator stopped');
                        statusBar.text = Constants.StatusBar.STOPPED;
                        break;
                    default:
                        log(LogLevel.Debug, `Status: ${state}${message ? ' - ' + message : ''}`);
                        break;
                }
            }
        });

        client.onNotification(Constants.Notifications.GENERATOR_ERROR, (params: any) => {
            const msg = params?.message ?? JSON.stringify(params);
            log(LogLevel.Error, `Gengora error: ${msg}`);
            vscode.window.showErrorMessage(`Gengora: ${msg}`);
        });

        // Dispose client on deactivation
        context.subscriptions.push({ dispose: () => client?.stop() });

        // Create explicit file watchers and forward to server
        // This ensures file watching works even if LSP dynamic registration isn't supported
        log(LogLevel.Info, 'Creating file watchers for **.cs, **.csproj, **.json');
        const csWatcher = vscode.workspace.createFileSystemWatcher('**/*.cs');
        const csprojWatcher = vscode.workspace.createFileSystemWatcher('**/*.csproj');
        const jsonWatcher = vscode.workspace.createFileSystemWatcher('**/*.json');

        const forwardFileChange = (uri: vscode.Uri, type: number) => {
            log(LogLevel.Info, `File change detected: ${uri.fsPath} (type=${type})`);
            if (client && client.isRunning()) {
                log(LogLevel.Info, `Forwarding to server...`);
                client.sendNotification('workspace/didChangeWatchedFiles', {
                    changes: [{ uri: uri.toString(), type }]
                });
            } else {
                log(LogLevel.Warning, 'Client not running, cannot forward file change');
            }
        };

        csWatcher.onDidChange((uri) => forwardFileChange(uri, 2)); // Changed
        csWatcher.onDidCreate((uri) => forwardFileChange(uri, 1)); // Created
        csWatcher.onDidDelete((uri) => forwardFileChange(uri, 3)); // Deleted

        csprojWatcher.onDidChange((uri) => forwardFileChange(uri, 2));
        csprojWatcher.onDidCreate((uri) => forwardFileChange(uri, 1));
        csprojWatcher.onDidDelete((uri) => forwardFileChange(uri, 3));

        jsonWatcher.onDidChange((uri) => forwardFileChange(uri, 2));
        jsonWatcher.onDidCreate((uri) => forwardFileChange(uri, 1));
        jsonWatcher.onDidDelete((uri) => forwardFileChange(uri, 3));

        context.subscriptions.push(csWatcher, csprojWatcher, jsonWatcher);
        log(LogLevel.Info, 'File watchers registered successfully');

        // Note: File watching is handled by explicit watchers above
        // The server filters based on observation mode

        // Commands
        context.subscriptions.push(vscode.commands.registerCommand(Constants.Commands.GENGORA_RUN, async () => {
            try {
                log(LogLevel.Info, 'Starting Gengora...');
                await context.workspaceState.update('gengora.manuallyStopped', false); // Clear manual stop flag
                if (client) {
                    await client.sendRequest(Constants.Methods.WORKSPACE_EXECUTE_COMMAND, { command: Constants.Commands.GENGORA_START });
                }
            } catch (error: any) {
                log(LogLevel.Error, `Failed to start: ${error?.message ?? error}`);
                vscode.window.showErrorMessage(`Failed to start Gengora: ${error?.message ?? error}`);
            }
        }));

        context.subscriptions.push(vscode.commands.registerCommand(Constants.Commands.GENGORA_SHOW_OUTPUT, () => {
            output.show(false);
        }));

        context.subscriptions.push(vscode.commands.registerCommand(Constants.Commands.GENGORA_STOP, async () => {
            try {
                log(LogLevel.Info, 'Stopping Gengora (manual stop - will persist across reloads)...');
                await context.workspaceState.update('gengora.manuallyStopped', true); // Set manual stop flag (persists across reloads)
                if (client) {
                    await client.sendRequest(Constants.Methods.WORKSPACE_EXECUTE_COMMAND, { command: Constants.Commands.GENGORA_STOP });
                }
            } catch (error: any) {
                log(LogLevel.Error, `Failed to stop: ${error?.message ?? error}`);
                vscode.window.showErrorMessage(`Failed to stop Gengora: ${error?.message ?? error}`);
            }
        }));

        // Auto-start if configured (NOTE: Server already auto-starts on initialization via OnInitialized)
        const autoRun = config.get<boolean>('autoRunOnCompileSuccess') ?? Constants.Defaults.AUTO_RUN_ON_COMPILE_SUCCESS;
        if (autoRun) {
            log(LogLevel.Info, 'Note: Auto-start setting is enabled but server already initializes automatically');
            log(LogLevel.Info, 'Consider disabling gengora.autoRunOnCompileSuccess setting to avoid confusion');
            /* Disabled to prevent double-build - server already calls StartGeneratorAsync in OnInitialized
            try {
                await new Promise(resolve => setTimeout(resolve, Constants.Defaults.AUTO_START_DELAY_MS));
                if (client && client.isRunning()) {
                    log(LogLevel.Debug, 'Sending auto-start command...');
                    await client.sendRequest(Constants.Methods.WORKSPACE_EXECUTE_COMMAND, { command: Constants.Commands.GENGORA_START });
                } else {
                    log(LogLevel.Warning, 'Client not ready for auto-start');
                }
            } catch (error: any) {
                log(LogLevel.Error, `Auto-start failed: ${error?.message ?? error}`);
            }
            */
        }

        log(LogLevel.Info, '=== Gengora Extension Activated ===');
        statusBar.text = Constants.StatusBar.READY;

    } catch (error: any) {
        const msg = `Activation failed: ${error?.message ?? error}`;
        log(LogLevel.Error, msg);
        vscode.window.showErrorMessage(`Gengora: ${msg}`);
        if (statusBar) {
            statusBar.text = Constants.StatusBar.ACTIVATION_FAILED;
        }
    }
}

export function deactivate(): Promise<void> | undefined {
    if (!client) return undefined;
    log(LogLevel.Info, 'Deactivating...');
    return client.stop() as Promise<void>;
}
