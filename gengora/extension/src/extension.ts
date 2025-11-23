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

let currentLogLevel: LogLevel = LogLevel.Info;

function log(level: LogLevel, message: string) {
    if (level <= currentLogLevel) {
        const prefix = ['[ERROR]', '[WARN]', '[INFO]', '[DEBUG]'][level];
        output.appendLine(`${prefix} ${message}`);
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
        const logLevelStr = config.get<string>('logLevel') || 'info';
        currentLogLevel = { error: LogLevel.Error, warning: LogLevel.Warning, info: LogLevel.Info, debug: LogLevel.Debug }[logLevelStr] ?? LogLevel.Info;

        log(LogLevel.Info, '=== Gengora Extension Activating ===');
        log(LogLevel.Debug, `Extension path: ${context.extensionPath}`);
        log(LogLevel.Debug, `Log level: ${logLevelStr}`);

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
            outputChannel: output
        };

        client = new LanguageClient('gengora', 'Gengora LSP', serverOptions, clientOptions);

        log(LogLevel.Debug, 'Starting language client...');
        await client.start();
        log(LogLevel.Info, 'Language client started');

        // Register notification handlers
        client.onNotification(Constants.Notifications.GENERATOR_STDOUT, (params: any) => {
            log(LogLevel.Debug, `[Gengora] ${params.text ?? String(params)}`);
        });

        client.onNotification(Constants.Notifications.GENERATOR_STDERR, (params: any) => {
            log(LogLevel.Warning, `[Gengora stderr] ${params.text ?? String(params)}`);
        });

        client.onNotification(Constants.Notifications.GENERATOR_STATUS, (params: any) => {
            const state = params?.state ?? '';
            log(LogLevel.Info, `Status: ${state}`);
            
            if (statusBar) {
                switch (state) {
                    case Constants.States.COMPILING:
                        statusBar.text = Constants.StatusBar.COMPILING;
                        break;
                    case Constants.States.COMPILED:
                        statusBar.text = Constants.StatusBar.COMPILED;
                        vscode.window.showInformationMessage('Gengora compiled successfully');
                        break;
                    case Constants.States.RUNNING:
                        statusBar.text = Constants.StatusBar.RUNNING;
                        break;
                    case Constants.States.ERROR:
                        statusBar.text = Constants.StatusBar.ERROR;
                        vscode.window.showErrorMessage('Gengora encountered an error');
                        break;
                    case Constants.States.STOPPED:
                        statusBar.text = Constants.StatusBar.STOPPED;
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

        // Note: File watching is now handled by the server based on observation modes
        // The server will dynamically watch files based on the discovered generator project

        // Commands
        context.subscriptions.push(vscode.commands.registerCommand(Constants.Commands.GENGORA_RUN, async () => {
            try {
                log(LogLevel.Info, 'Starting Gengora...');
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
                log(LogLevel.Info, 'Stopping Gengora...');
                if (client) {
                    await client.sendRequest(Constants.Methods.WORKSPACE_EXECUTE_COMMAND, { command: Constants.Commands.GENGORA_STOP });
                }
            } catch (error: any) {
                log(LogLevel.Error, `Failed to stop: ${error?.message ?? error}`);
                vscode.window.showErrorMessage(`Failed to stop Gengora: ${error?.message ?? error}`);
            }
        }));

        // Auto-start if configured
        const autoRun = config.get<boolean>('autoRunOnCompileSuccess') ?? Constants.Defaults.AUTO_RUN_ON_COMPILE_SUCCESS;
        if (autoRun) {
            log(LogLevel.Info, 'Auto-start enabled');
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
