"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = __importStar(require("vscode"));
const path = __importStar(require("path"));
const node_1 = require("vscode-languageclient/node");
let client;
let statusBarItem;
let outputChannel;
let currentState = 'Idle';
let extensionContext;
// State Icons
const STATE_ICONS = {
    'Idle': '$(circle-slash)',
    'GeneratorFound': '$(search)',
    'Compiling': '$(sync~spin)',
    'Ready': '$(check)',
    'Running': '$(play)',
    'Error': '$(error)',
    'Stopped': '$(debug-stop)'
};
// State Colors
const STATE_COLORS = {
    'Idle': undefined,
    'GeneratorFound': new vscode.ThemeColor('statusBarItem.warningForeground'),
    'Compiling': new vscode.ThemeColor('statusBarItem.warningForeground'),
    'Ready': new vscode.ThemeColor('statusBarItem.prominentForeground'),
    'Running': new vscode.ThemeColor('statusBarItem.prominentForeground'),
    'Error': new vscode.ThemeColor('statusBarItem.errorForeground'),
    'Stopped': undefined
};
// Log Levels
const LOG_LEVELS = ['trace', 'debug', 'info', 'warning', 'error'];
async function activate(context) {
    extensionContext = context;
    outputChannel = vscode.window.createOutputChannel('Gengora');
    context.subscriptions.push(outputChannel);
    // Create Status Bar Item - Now With Quick Pick Menu
    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    statusBarItem.command = 'gengora.showQuickPick';
    context.subscriptions.push(statusBarItem);
    updateStatusBar('Idle');
    // Register Commands
    context.subscriptions.push(vscode.commands.registerCommand('gengora.showQuickPick', showQuickPickMenu), vscode.commands.registerCommand('gengora.start', startCommand), vscode.commands.registerCommand('gengora.recompile', recompileCommand), vscode.commands.registerCommand('gengora.stop', stopCommand), vscode.commands.registerCommand('gengora.restart', restartCommand), vscode.commands.registerCommand('gengora.showOutput', () => outputChannel.show()), vscode.commands.registerCommand('gengora.setLogLevel', setLogLevelCommand));
    // Check If Auto-Start Is Enabled
    const config = vscode.workspace.getConfiguration('gengora');
    const autoStart = config.get('autoStart', true);
    if (autoStart) {
        await startLanguageServer(context);
    }
    outputChannel.appendLine('Gengora Extension Activated');
}
function deactivate() {
    if (!client) {
        return undefined;
    }
    return client.stop();
}
/**
 * Shows the quick pick menu when clicking on the status bar icon.
 * Provides access to all Gengora commands and settings.
 */
async function showQuickPickMenu() {
    const config = vscode.workspace.getConfiguration('gengora');
    const currentLogLevel = config.get('logLevel', 'debug');
    const isServerRunning = client !== undefined;
    const items = [
        {
            label: '$(output) Show Output',
            description: 'Open the Gengora output channel',
            action: 'showOutput'
        },
        {
            label: '',
            kind: vscode.QuickPickItemKind.Separator,
            action: ''
        }
    ];
    // Add state-dependent commands
    if (!isServerRunning) {
        items.push({
            label: '$(play) Start Server',
            description: 'Start the Gengora language server',
            action: 'start'
        });
    }
    else {
        items.push({
            label: '$(refresh) Recompile Generator',
            description: 'Force recompilation of the generator',
            action: 'recompile'
        }, {
            label: '$(debug-stop) Stop Generator',
            description: 'Stop the current generator',
            action: 'stop'
        }, {
            label: '$(debug-restart) Restart Server',
            description: 'Restart the language server',
            action: 'restart'
        });
    }
    items.push({
        label: '',
        kind: vscode.QuickPickItemKind.Separator,
        action: ''
    }, {
        label: `$(settings-gear) Log Level: ${currentLogLevel}`,
        description: 'Change the logging verbosity',
        action: 'setLogLevel'
    }, {
        label: '',
        kind: vscode.QuickPickItemKind.Separator,
        action: ''
    }, {
        label: `$(info) Status: ${currentState}`,
        description: 'Current generator state',
        action: 'info'
    });
    const selected = await vscode.window.showQuickPick(items, {
        placeHolder: 'Gengora Commands',
        title: 'Gengora - Live Code Generation'
    });
    if (!selected) {
        return;
    }
    switch (selected.action) {
        case 'showOutput':
            outputChannel.show();
            break;
        case 'start':
            await vscode.commands.executeCommand('gengora.start');
            break;
        case 'recompile':
            await vscode.commands.executeCommand('gengora.recompile');
            break;
        case 'stop':
            await vscode.commands.executeCommand('gengora.stop');
            break;
        case 'restart':
            await vscode.commands.executeCommand('gengora.restart');
            break;
        case 'setLogLevel':
            await vscode.commands.executeCommand('gengora.setLogLevel');
            break;
        case 'info':
            vscode.window.showInformationMessage(`Gengora State: ${currentState}`);
            break;
    }
}
/**
 * Command to change the log level.
 */
async function setLogLevelCommand() {
    const config = vscode.workspace.getConfiguration('gengora');
    const currentLogLevel = config.get('logLevel', 'debug');
    const items = LOG_LEVELS.map(level => ({
        label: level.charAt(0).toUpperCase() + level.slice(1),
        description: level === currentLogLevel ? '(current)' : undefined,
        picked: level === currentLogLevel,
        level
    }));
    const selected = await vscode.window.showQuickPick(items, {
        placeHolder: 'Select Log Level',
        title: 'Gengora Log Level'
    });
    if (selected) {
        await config.update('logLevel', selected.level, vscode.ConfigurationTarget.Global);
        outputChannel.appendLine(`Log level changed to: ${selected.level}`);
        vscode.window.showInformationMessage(`Gengora: Log level set to ${selected.level}`);
        // Notify server of log level change if connected
        if (client) {
            try {
                await client.sendNotification('gengora/setLogLevel', { level: selected.level });
            }
            catch {
                // Server might not support this notification yet
            }
        }
    }
}
/**
 * Command to start the language server.
 */
async function startCommand() {
    if (client) {
        vscode.window.showInformationMessage('Gengora: Server is already running');
        return;
    }
    await startLanguageServer(extensionContext);
    vscode.window.showInformationMessage('Gengora: Server started');
}
/**
 * Command to restart the language server.
 */
async function restartCommand() {
    if (client) {
        await client.stop();
        client = undefined;
    }
    await startLanguageServer(extensionContext);
    vscode.window.showInformationMessage('Gengora: Server restarted');
}
async function startLanguageServer(context) {
    const config = vscode.workspace.getConfiguration('gengora');
    // Determine Server Path
    let serverPath = config.get('serverPath', '');
    if (!serverPath) {
        // Use Bundled Server
        serverPath = context.asAbsolutePath(path.join('server', 'Gengora.Server.dll'));
    }
    outputChannel.appendLine(`Server Path: ${serverPath}`);
    // Server Options
    const serverOptions = {
        run: {
            command: 'dotnet',
            args: [serverPath],
            transport: node_1.TransportKind.stdio
        },
        debug: {
            command: 'dotnet',
            args: [serverPath],
            transport: node_1.TransportKind.stdio
        }
    };
    // Client Options
    const clientOptions = {
        documentSelector: [{ scheme: 'file', language: 'csharp' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.cs')
        },
        outputChannel: outputChannel,
        initializationOptions: {
            capabilities: {
                statusBar: true,
                diagnostics: true
            },
            logLevel: config.get('logLevel', 'debug')
        }
    };
    // Create And Start Client
    client = new node_1.LanguageClient('gengora', 'Gengora Language Server', serverOptions, clientOptions);
    // Handle Notifications
    client.onNotification('gengora/stateChanged', handleStateChanged);
    client.onNotification('gengora/diagnostics', handleDiagnostics);
    client.onNotification('gengora/fileEmitted', handleFileEmitted);
    // Start Client
    await client.start();
    outputChannel.appendLine('Language Server Started');
}
function updateStatusBar(state, message) {
    currentState = state;
    const icon = STATE_ICONS[state] ?? '$(question)';
    const displayText = message ?? state;
    statusBarItem.text = `${icon} Gengora: ${displayText}`;
    statusBarItem.color = STATE_COLORS[state];
    statusBarItem.tooltip = `Gengora Generator State: ${state}${message ? `\n${message}` : ''}\n\nClick for commands`;
    statusBarItem.show();
}
function handleStateChanged(notification) {
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
function handleDiagnostics(notification) {
    outputChannel.appendLine(`Received ${notification.diagnostics.length} Diagnostic(s)`);
    for (const diagnostic of notification.diagnostics) {
        const location = diagnostic.filePath
            ? `${diagnostic.filePath}:${diagnostic.line}:${diagnostic.column}`
            : '(unknown)';
        outputChannel.appendLine(`  [${diagnostic.severity}] ${diagnostic.id}: ${diagnostic.message} at ${location}`);
    }
    if (notification.isCompilationError) {
        vscode.window.showErrorMessage(`Gengora: Compilation Failed With ${notification.diagnostics.length} Error(s)`, 'Show Output').then(selection => {
            if (selection === 'Show Output') {
                outputChannel.show();
            }
        });
    }
}
function handleFileEmitted(notification) {
    outputChannel.appendLine(`File Emitted: ${notification.path}`);
}
async function recompileCommand() {
    if (!client) {
        vscode.window.showWarningMessage('Gengora: Language Server Not Running');
        return;
    }
    try {
        const result = await client.sendRequest('gengora/recompile');
        if (result.success) {
            vscode.window.showInformationMessage('Gengora: Recompilation Succeeded');
        }
        else {
            vscode.window.showErrorMessage(`Gengora: ${result.message ?? 'Recompilation Failed'}`);
        }
    }
    catch (error) {
        vscode.window.showErrorMessage(`Gengora: ${error}`);
    }
}
async function stopCommand() {
    if (!client) {
        vscode.window.showWarningMessage('Gengora: Language Server Not Running');
        return;
    }
    try {
        await client.sendRequest('gengora/stop');
        vscode.window.showInformationMessage('Gengora: Generator Stopped');
    }
    catch (error) {
        vscode.window.showErrorMessage(`Gengora: ${error}`);
    }
}
//# sourceMappingURL=extension.js.map