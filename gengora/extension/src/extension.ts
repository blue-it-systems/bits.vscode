import * as path from 'path';
import * as fs from 'fs';
import * as vscode from 'vscode';
import { LanguageClient, TransportKind } from 'vscode-languageclient/node';
import { spawn, ChildProcessWithoutNullStreams } from 'child_process';
import { minimatch } from 'minimatch';

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
        statusBar.text = '$(sync~spin) Gengora: initializing';
        statusBar.tooltip = 'Gengora - Click to show output';
        statusBar.command = 'gengora.showOutput';
        statusBar.show();
        context.subscriptions.push(statusBar);

        // Find workspace root
        const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
        if (!workspaceRoot) {
            const msg = 'No workspace folder opened. Please open a folder containing your generator project.';
            log(LogLevel.Error, msg);
            vscode.window.showErrorMessage(msg);
            statusBar.text = '$(error) Gengora: No workspace';
            return;
        }

        log(LogLevel.Debug, `Workspace root: ${workspaceRoot}`);

        // Find generator folder
        const configuredGeneratorPath = config.get<string>('generatorFolderPath') || 'Gengora';
        const generatorRoot = findGeneratorFolder(workspaceRoot, configuredGeneratorPath);
        
        if (!generatorRoot) {
            const msg = `Generator folder "${configuredGeneratorPath}" not found or missing .csproj file`;
            log(LogLevel.Error, msg);
            vscode.window.showErrorMessage(`Gengora: ${msg}`);
            statusBar.text = '$(error) Gengora: No generator project';
            return;
        }

        // Find server DLL
        let serverPath = config.get<string>('serverPath') || '';
        if (!serverPath) {
            serverPath = path.join(context.extensionPath, '..', 'server', 'bin', 'Debug', 'net8.0', 'BITS.Gengora.Server.dll');
        }

        if (!fs.existsSync(serverPath)) {
            const msg = `Server not found at ${serverPath}. Please build the server project.`;
            log(LogLevel.Error, msg);
            vscode.window.showErrorMessage(msg);
            statusBar.text = '$(error) Gengora: Server missing';
            return;
        }

        log(LogLevel.Info, `Server: ${serverPath}`);
        log(LogLevel.Info, `Generator project: ${generatorRoot}`);

        // Start language server
        const isDll = serverPath.toLowerCase().endsWith('.dll');
        const serverOptions = isDll 
            ? { command: 'dotnet', args: [serverPath, generatorRoot], transport: TransportKind.stdio }
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
        client.onNotification('$/generator.stdout', (params: any) => {
            log(LogLevel.Debug, `[Generator] ${params.text ?? String(params)}`);
        });

        client.onNotification('$/generator.stderr', (params: any) => {
            log(LogLevel.Warning, `[Generator stderr] ${params.text ?? String(params)}`);
        });

        client.onNotification('$/generator.status', (params: any) => {
            const state = params?.state ?? '';
            log(LogLevel.Info, `Status: ${state}`);
            
            if (statusBar) {
                switch (state) {
                    case 'compiling':
                        statusBar.text = '$(sync~spin) Gengora: compiling';
                        break;
                    case 'compiled':
                        statusBar.text = '$(check) Gengora: compiled';
                        vscode.window.showInformationMessage('Gengora compiled successfully');
                        break;
                    case 'running':
                        statusBar.text = '$(play) Gengora: running';
                        break;
                    case 'error':
                        statusBar.text = '$(error) Gengora: error';
                        vscode.window.showErrorMessage('Gengora encountered an error');
                        break;
                    case 'stopped':
                        statusBar.text = '$(debug-stop) Gengora: stopped';
                        break;
                }
            }
        });

        client.onNotification('generator/error', (params: any) => {
            const msg = params?.message ?? JSON.stringify(params);
            log(LogLevel.Error, `Generator error: ${msg}`);
            vscode.window.showErrorMessage(`Gengora: ${msg}`);
        });

        // Dispose client on deactivation
        context.subscriptions.push({ dispose: () => client?.stop() });

        // File watchers - watch only the generator folder
        const ignorePatterns = config.get<string[]>('ignorePatterns') || [];
        const csWatcher = vscode.workspace.createFileSystemWatcher(new vscode.RelativePattern(generatorRoot, '**/*.cs'));
        const csprojWatcher = vscode.workspace.createFileSystemWatcher(new vscode.RelativePattern(generatorRoot, '**/*.csproj'));

        const forwardChange = (uri: vscode.Uri, type: number) => {
            if (!client) return;
            
            if (shouldIgnorePath(uri.fsPath, generatorRoot, ignorePatterns)) {
                log(LogLevel.Debug, `Ignored: ${path.relative(generatorRoot, uri.fsPath)}`);
                return;
            }

            log(LogLevel.Debug, `File changed: ${path.relative(generatorRoot, uri.fsPath)}`);
            client.sendNotification('workspace/didChangeWatchedFiles', { 
                changes: [{ uri: uri.toString(), type }] 
            });
        };

        csWatcher.onDidChange((uri) => forwardChange(uri, 2));
        csWatcher.onDidCreate((uri) => forwardChange(uri, 1));
        csWatcher.onDidDelete((uri) => forwardChange(uri, 3));
        csprojWatcher.onDidChange((uri) => forwardChange(uri, 2));
        csprojWatcher.onDidCreate((uri) => forwardChange(uri, 1));
        csprojWatcher.onDidDelete((uri) => forwardChange(uri, 3));

        context.subscriptions.push(csWatcher, csprojWatcher);

        // Commands
        context.subscriptions.push(vscode.commands.registerCommand('gengora.run', async () => {
            try {
                log(LogLevel.Info, 'Starting generator...');
                if (client) {
                    await client.sendRequest('workspace/executeCommand', { command: 'gengora.start' });
                }
            } catch (error: any) {
                log(LogLevel.Error, `Failed to start: ${error?.message ?? error}`);
                vscode.window.showErrorMessage(`Failed to start generator: ${error?.message ?? error}`);
            }
        }));

        context.subscriptions.push(vscode.commands.registerCommand('gengora.showOutput', () => {
            output.show(false);
        }));

        context.subscriptions.push(vscode.commands.registerCommand('gengora.stop', async () => {
            try {
                log(LogLevel.Info, 'Stopping generator...');
                if (client) {
                    await client.sendRequest('workspace/executeCommand', { command: 'gengora.stop' });
                }
            } catch (error: any) {
                log(LogLevel.Error, `Failed to stop: ${error?.message ?? error}`);
                vscode.window.showErrorMessage(`Failed to stop generator: ${error?.message ?? error}`);
            }
        }));

        // Auto-start if configured
        const autoRun = config.get<boolean>('autoRunOnCompileSuccess') ?? true;
        if (autoRun) {
            log(LogLevel.Info, 'Auto-start enabled');
            try {
                await new Promise(resolve => setTimeout(resolve, 500));
                if (client && client.isRunning()) {
                    log(LogLevel.Debug, 'Sending auto-start command...');
                    await client.sendRequest('workspace/executeCommand', { command: 'gengora.start' });
                } else {
                    log(LogLevel.Warning, 'Client not ready for auto-start');
                }
            } catch (error: any) {
                log(LogLevel.Error, `Auto-start failed: ${error?.message ?? error}`);
            }
        }

        log(LogLevel.Info, '=== Gengora Extension Activated ===');
        statusBar.text = '$(check) Gengora: ready';

    } catch (error: any) {
        const msg = `Activation failed: ${error?.message ?? error}`;
        log(LogLevel.Error, msg);
        vscode.window.showErrorMessage(`Gengora: ${msg}`);
        if (statusBar) {
            statusBar.text = '$(error) Gengora: activation failed';
        }
    }
}

export function deactivate(): Promise<void> | undefined {
    if (!client) return undefined;
    log(LogLevel.Info, 'Deactivating...');
    return client.stop() as Promise<void>;
}
