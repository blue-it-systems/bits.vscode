import * as path from 'path';
import * as fs from 'fs';
import * as vscode from 'vscode';
import { LanguageClient, TransportKind } from 'vscode-languageclient/node';
import { spawn, ChildProcessWithoutNullStreams } from 'child_process';
import { minimatch } from 'minimatch';
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

function findGeneratorFolder(workspaceRoot: string, configuredPath: string): string | null {
    try {
        // Try configured path first (absolute or relative to workspace)
        const candidatePath = path.isAbsolute(configuredPath) 
            ? configuredPath 
            : path.join(workspaceRoot, configuredPath);
        
        if (fs.existsSync(candidatePath)) {
            // Check if it contains a .csproj file
            const files = fs.readdirSync(candidatePath);
            const hasCsproj = files.some(f => f.endsWith('.csproj'));
            if (hasCsproj) {
                log(LogLevel.Info, `Found generator folder: ${candidatePath}`);
                return candidatePath;
            } else {
                log(LogLevel.Warning, `Folder ${candidatePath} exists but contains no .csproj file`);
            }
        }
        
        log(LogLevel.Error, `Generator folder not found: ${candidatePath}`);
        return null;
    } catch (error: any) {
        log(LogLevel.Error, `Error finding generator folder: ${error?.message ?? error}`);
        return null;
    }
}

function shouldIgnorePath(filePath: string, generatorRoot: string, ignorePatterns: string[]): boolean {
    try {
        const relativePath = path.relative(generatorRoot, filePath);
        
        for (const pattern of ignorePatterns) {
            if (minimatch(relativePath, pattern, { dot: true })) {
                return true;
            }
        }
        return false;
    } catch (error: any) {
        log(LogLevel.Debug, `Error checking ignore patterns: ${error?.message ?? error}`);
        return false;
    }
}

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

        // Find generator folder or project
        const configuredGeneratorPath = config.get<string>('generatorFolderPath') || Constants.Defaults.GENERATOR_FOLDER_PATH;
        const configuredProjectPath = config.get<string>('generatorProjectPath') || '';
        
        const generatorRoot = findGeneratorFolder(workspaceRoot, configuredGeneratorPath);
        
        if (!generatorRoot) {
            const msg = `Generator folder "${configuredGeneratorPath}" not found or missing .csproj file`;
            log(LogLevel.Error, msg);
            vscode.window.showErrorMessage(`Gengora: ${msg}`);
            statusBar.text = Constants.StatusBar.NO_GENERATOR;
            return;
        }

        // Find server DLL
        let serverPath = config.get<string>('serverPath') || '';
        if (!serverPath) {
            serverPath = path.join(context.extensionPath, '..', 'server', 'bin', Constants.Build.DEBUG_CONFIG, Constants.Build.TARGET_FRAMEWORK, Constants.Build.SERVER_DLL_NAME);
        }

        if (!fs.existsSync(serverPath)) {
            const msg = `Server not found at ${serverPath}. Please build the server project.`;
            log(LogLevel.Error, msg);
            vscode.window.showErrorMessage(msg);
            statusBar.text = Constants.StatusBar.SERVER_MISSING;
            return;
        }

        log(LogLevel.Info, `Server: ${serverPath}`);
        log(LogLevel.Info, `Generator project: ${generatorRoot}`);

        // Start language server with environment variables for configuration
        const isDll = serverPath.toLowerCase().endsWith('.dll');
        const serverEnv = {
            ...process.env,
            GENERATOR_FOLDER_PATH: configuredGeneratorPath,
            ...(configuredProjectPath && { GENERATOR_PROJECT_PATH: configuredProjectPath })
        };
        
        const serverOptions = isDll 
            ? { 
                command: 'dotnet', 
                args: [serverPath, Constants.CliArgs.WORKSPACE_ROOT, generatorRoot], 
                transport: TransportKind.stdio,
                options: { env: serverEnv }
            }
            : { command: serverPath, args: [generatorRoot], transport: TransportKind.stdio };

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

        // File watchers - watch only the generator folder
        const ignorePatterns = config.get<string[]>('ignorePatterns') || [...Constants.Defaults.IGNORE_PATTERNS];
        const csWatcher = vscode.workspace.createFileSystemWatcher(new vscode.RelativePattern(generatorRoot, Constants.FilePatterns.CSHARP));
        const csprojWatcher = vscode.workspace.createFileSystemWatcher(new vscode.RelativePattern(generatorRoot, Constants.FilePatterns.CSPROJ));

        const forwardChange = (uri: vscode.Uri, type: number) => {
            if (!client) return;
            
            if (shouldIgnorePath(uri.fsPath, generatorRoot, ignorePatterns)) {
                log(LogLevel.Debug, `Ignored: ${path.relative(generatorRoot, uri.fsPath)}`);
                return;
            }

            log(LogLevel.Debug, `File changed: ${path.relative(generatorRoot, uri.fsPath)}`);
            client.sendNotification(Constants.Methods.WORKSPACE_DID_CHANGE_WATCHED_FILES, { 
                changes: [{ uri: uri.toString(), type }] 
            });
        };

        csWatcher.onDidChange((uri) => forwardChange(uri, Constants.FileChangeType.CHANGED));
        csWatcher.onDidCreate((uri) => forwardChange(uri, Constants.FileChangeType.CREATED));
        csWatcher.onDidDelete((uri) => forwardChange(uri, Constants.FileChangeType.DELETED));
        csprojWatcher.onDidChange((uri) => forwardChange(uri, Constants.FileChangeType.CHANGED));
        csprojWatcher.onDidCreate((uri) => forwardChange(uri, Constants.FileChangeType.CREATED));
        csprojWatcher.onDidDelete((uri) => forwardChange(uri, Constants.FileChangeType.DELETED));

        context.subscriptions.push(csWatcher, csprojWatcher);

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
