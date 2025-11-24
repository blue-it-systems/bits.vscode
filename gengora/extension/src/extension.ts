import * as path from 'path';
import * as fs from 'fs';
import * as vscode from 'vscode';
import { LanguageClient, TransportKind } from 'vscode-languageclient/node';
import * as Constants from './constants';

let client: LanguageClient | undefined;
let output: vscode.OutputChannel;
let statusBar: vscode.StatusBarItem | undefined;
let isActivated: boolean = false; // Guard against duplicate activation

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

// File watcher management for dynamic registration
let fileWatchers: Map<string, vscode.FileSystemWatcher> = new Map();

/**
 * Parses .gitignore patterns and converts them to glob patterns
 */
function parseGitignorePatterns(gitignorePath: string): string[] {
    const patterns: string[] = [];
    try {
        if (!fs.existsSync(gitignorePath)) {
            return patterns;
        }
        
        const content = fs.readFileSync(gitignorePath, 'utf-8');
        const lines = content.split('\n');
        
        for (const line of lines) {
            const trimmed = line.trim();
            // Skip empty lines and comments
            if (!trimmed || trimmed.startsWith('#')) {
                continue;
            }
            
            // Convert gitignore pattern to glob pattern
            // Remove leading/trailing slashes
            let pattern = trimmed.replace(/^\/+|\/+$/g, '');
            
            // If pattern ends with /, it's a directory
            if (pattern.endsWith('/')) {
                pattern = `**/${pattern}**`;
            } else if (!pattern.includes('*') && !pattern.includes('?')) {
                // Literal file/folder - make it match anywhere and as directory
                pattern = `**/${pattern}` + (trimmed.endsWith('/') ? '**' : '');
            } else {
                // Already has wildcards, make it match anywhere
                if (!pattern.startsWith('*')) {
                    pattern = `**/${pattern}`;
                }
            }
            
            patterns.push(pattern);
        }
    } catch (error) {
        // Silently fail - gitignore not found or error reading
    }
    
    return patterns;
}

/**
 * Merges user-provided patterns with gitignore patterns
 */
function mergeExcludePatterns(userPatterns: string[], generatorProjectFolder: string, includeGitignore: boolean): string[] {
    const allPatterns = new Set(userPatterns);
    
    if (includeGitignore && generatorProjectFolder) {
        const gitignorePath = path.join(generatorProjectFolder, '.gitignore');
        const gitignorePatterns = parseGitignorePatterns(gitignorePath);
        gitignorePatterns.forEach(p => allPatterns.add(p));
    }
    
    return Array.from(allPatterns);
}

/**
 * Disposes all active file watchers
 */
function disposeAllWatchers(logger: (msg: string) => void) {
    for (const [pattern, watcher] of fileWatchers) {
        logger(`Disposing watcher for pattern: ${pattern}`);
        watcher.dispose();
    }
    fileWatchers.clear();
}

/**
 * Creates file watchers for specified patterns
 */
function createWatchers(
    patterns: string[],
    projectFolder: string | undefined,
    excludePatterns: string[],
    logger: (msg: string) => void
): Map<string, vscode.FileSystemWatcher> {
    const watchers = new Map<string, vscode.FileSystemWatcher>();
    
    for (const pattern of patterns) {
        // If a project folder is provided, create a RelativePattern to scope watchers to the project folder
        const glob = projectFolder ? new vscode.RelativePattern(projectFolder, pattern) : pattern;
        const watcher = vscode.workspace.createFileSystemWatcher(glob as any);
        watchers.set(pattern, watcher);
        logger(`Created watcher for pattern: ${pattern}`);
    }
    
    return watchers;
}

export async function activate(context: vscode.ExtensionContext) {
    // Guard against duplicate activation
    if (isActivated) {
        console.warn('[Gengora] Extension already activated, skipping duplicate activation');
        return;
    }
    
    try {
        isActivated = true; // Mark as activated immediately to prevent race conditions
        
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

        // Status bar with menu command
        statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
        statusBar.text = Constants.StatusBar.INITIALIZING;
        statusBar.tooltip = Constants.StatusBar.TOOLTIP;
        statusBar.command = 'gengora.statusBarMenu';
        statusBar.show();
        context.subscriptions.push(statusBar);

        // Find workspace root (we'll prefer the generator project folder if we auto-discover it)
        let workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
        if (!workspaceRoot) {
            const msg = 'No workspace folder opened. Please open a folder containing your generator project.';
            log(LogLevel.Error, msg);
            vscode.window.showErrorMessage(msg);
            statusBar.text = Constants.StatusBar.NO_WORKSPACE;
            return;
        }

        log(LogLevel.Debug, `Workspace root: ${workspaceRoot}`);

        // Get user-specified project path (if any)
        let configuredProjectPath = config.get<string>('generatorProjectPath') || '';
        
        if (configuredProjectPath) {
            log(LogLevel.Info, `Using configured generator project path: ${configuredProjectPath}`);
        } else {
            log(LogLevel.Info, 'Auto-discovering generator project via marker...');
        }

        // If not explicitly configured, scan ALL workspace folders for a .csproj that contains the generator marker
        if (!configuredProjectPath) {
            try {
                const marker = Constants.Patterns.GENERATOR_PROJECT_MARKER.toLowerCase();
                const candidates = await vscode.workspace.findFiles('**/*.csproj', undefined, 500);

                // Prefer any candidate within the primary workspaceRoot folder
                let found: string | undefined;

                for (const uri of candidates) {
                    try {
                        const content = fs.readFileSync(uri.fsPath, 'utf-8');
                        if (content.toLowerCase().includes(marker)) {
                            // Save first candidate
                            found = uri.fsPath;
                            // If found inside primary workspace folder, pick it immediately
                            if (workspaceRoot && uri.fsPath.startsWith(workspaceRoot)) {
                                break;
                            }
                        }
                    } catch (e) {
                        // ignore read errors
                    }
                }

                if (found) {
                    configuredProjectPath = found;
                    // prefer server workspace root to be the project folder for accurate scanning
                    workspaceRoot = path.dirname(found);
                    log(LogLevel.Info, `Auto-discovered generator project: ${found}`);
                }
            } catch (err) {
                log(LogLevel.Debug, `Auto-discovery scan failed: ${String(err)}`);
            }
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
            ...(configuredProjectPath && { GENERATOR_PROJECT_PATH: configuredProjectPath }),
            GENGORA_MANUALLY_STOPPED: isManuallyStopped ? 'true' : 'false'
        };
        
        const serverOptions = isDll 
            ? { 
                command: 'dotnet', 
                args: [serverPath, Constants.CliArgs.WORKSPACE_ROOT, workspaceRoot], 
                transport: TransportKind.stdio,
                options: { env: serverEnv, cwd: workspaceRoot }
            }
            : { 
                command: serverPath, 
                args: [Constants.CliArgs.WORKSPACE_ROOT, workspaceRoot], 
                transport: TransportKind.stdio,
                options: { env: serverEnv, cwd: workspaceRoot }
            };

        const clientOptions = {
            documentSelector: [{ scheme: 'file' }],
            outputChannel: new FilteredOutputChannel('Gengora LSP', output)
        };

        client = new LanguageClient('gengora', 'Gengora LSP', serverOptions, clientOptions);

        log(LogLevel.Debug, 'Starting language client...');
        await client.start();
        log(LogLevel.Info, 'Language client started');

        // Server will handle auto-start on initialization. The extension no longer sends an explicit start command
        // to avoid duplicate start requests. Server respects initial workspace state regarding manual stop.

        // Register notification handlers
        client.onNotification(Constants.Notifications.GENERATOR_STDOUT, (params: any) => {
            log(LogLevel.Debug, `[Gengora] ${params.text ?? String(params)}`);
        });

        client.onNotification(Constants.Notifications.GENERATOR_STDERR, (params: any) => {
            log(LogLevel.Warning, `[Gengora stderr] ${params.text ?? String(params)}`);
        });

        // Structured notifications from generators - handshake and generated events
        client.onNotification(Constants.Notifications.GENERATOR_HELLO, (params: any) => {
            try {
                log(LogLevel.Info, `[Generator hello] capabilities: ${JSON.stringify(params?.capabilities ?? params)} `);
            } catch (e) {
                log(LogLevel.Debug, `[Generator hello] ${String(params)}`);
            }
        });

        client.onNotification(Constants.Notifications.GENERATOR_GENERATED, (params: any) => {
            const project = params?.project ?? params?.projectPath ?? '';
            const createdRaw = params?.created ?? params?.files ?? [];
            const created = Array.isArray(createdRaw) ? createdRaw : [createdRaw].filter(Boolean);

            if (created.length > 0) {
                log(LogLevel.Info, `[Generator] Created ${created.length} file(s) under ${project}`);
                for (const f of created) {
                    log(LogLevel.Info, `  • ${f}`);
                }

                // Give user a gentle hint so they can view results
                const actionShow = 'Show Output (Gengora)';
                const actionReveal = 'Reveal First File';
                vscode.window.showInformationMessage(`Gengora: generator produced ${created.length} file(s)`, actionShow, actionReveal)
                    .then(choice => {
                        if (!choice) return;
                        if (choice === actionShow) {
                            output.show(true);
                        } else if (choice === actionReveal && created.length > 0) {
                            try {
                                const uri = vscode.Uri.file(created[0]);
                                vscode.commands.executeCommand('revealFileInOS', uri);
                            } catch (e) {
                                output.appendLine(`[WARN] failed to reveal file: ${String(e)}`);
                            }
                        }
                    });
            } else {
                log(LogLevel.Info, `[Generator] Created event received for project ${project} (no files listed)`);
            }
        });

        client.onNotification(Constants.Notifications.GENERATOR_PROJECT_DISCOVERED, (params: any) => {
            const projectPath = params?.projectPath ?? '';
            if (projectPath) {
                log(LogLevel.Info, `Generator project discovered: ${projectPath}`);
            }
        });

        client.onNotification(Constants.Notifications.OBSERVATION_MODE_CHANGED, (params: any) => {
            const mode = params?.mode ?? 'unknown';
            const projectFolder = params?.projectFolder ?? '';
            
            log(LogLevel.Debug, `Observation mode changed to: ${mode}`);
            
            if (mode === 'FullObservation' && projectFolder) {
                // Generator found - setup full file watching
                log(LogLevel.Info, `Setting up full file watchers for: ${projectFolder}`);
                const config = vscode.workspace.getConfiguration('gengora');
                const userPatterns = config.get<string[]>(Constants.ConfigKeys.EXCLUDE_PATTERNS) || Array.from(Constants.Defaults.IGNORE_PATTERNS);
                const mergeGitignore = config.get<boolean>(Constants.ConfigKeys.MERGE_GITIGNORE) ?? true;
                const excludePatterns = mergeExcludePatterns(userPatterns, projectFolder, mergeGitignore);
                
                if (mergeGitignore) {
                    log(LogLevel.Debug, `Merged .gitignore patterns (${excludePatterns.length} total patterns)`);
                }
                
                // Dispose old watchers
                disposeAllWatchers((msg) => log(LogLevel.Debug, msg));
                
                // Create new watchers for full observation
                const patterns = ['**/*.cs', '**/*.csproj', '**/*.json'];
                fileWatchers = createWatchers(patterns, projectFolder, excludePatterns, (msg) => log(LogLevel.Debug, msg));
                // Attach handlers for these new watchers so changes are forwarded
                attachWatcherHandlers();
                context.subscriptions.push(...Array.from(fileWatchers.values()));
            } else if (mode === 'MinimalObservation') {
                // Generator not found - only watch for .csproj files
                log(LogLevel.Info, 'Switching to minimal file watching (only .csproj files)');
                
                disposeAllWatchers((msg) => log(LogLevel.Debug, msg));
                fileWatchers = createWatchers(['**/*.csproj'], undefined, [], (msg) => log(LogLevel.Debug, msg));
                attachWatcherHandlers();
                context.subscriptions.push(...Array.from(fileWatchers.values()));
            }
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
                        log(LogLevel.Debug, '[Gengora] Generator running');
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
                    case Constants.States.OBSERVING_MINIMAL:
                        log(LogLevel.Debug, '[Gengora] Observing (minimal - waiting for marker)');
                        statusBar.text = Constants.StatusBar.READY;
                        break;
                    case Constants.States.OBSERVING_FULL:
                        log(LogLevel.Debug, '[Gengora] Observing (full - generator project found)');
                        statusBar.text = Constants.StatusBar.READY;
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
        context.subscriptions.push({ dispose: () => {
            client?.stop();
            disposeAllWatchers((msg) => log(LogLevel.Debug, msg));
        }});

        // Setup file change forwarding
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

        // Initialize watchers based on pre-start discovery
        if (configuredProjectPath) {
            // We auto-discovered a generator project before language client started — create full watchers scoped to that project
            const projectFolder = path.dirname(configuredProjectPath);
            log(LogLevel.Info, `Initializing full file watchers for discovered project: ${projectFolder}`);
            const config = vscode.workspace.getConfiguration('gengora');
            const userPatterns = config.get<string[]>(Constants.ConfigKeys.EXCLUDE_PATTERNS) || Array.from(Constants.Defaults.IGNORE_PATTERNS);
            const mergeGitignore = config.get<boolean>(Constants.ConfigKeys.MERGE_GITIGNORE) ?? true;
            const excludePatterns = mergeExcludePatterns(userPatterns, projectFolder, mergeGitignore);

            const patterns = ['**/*.cs', '**/*.csproj', '**/*.json'];
            fileWatchers = createWatchers(patterns, projectFolder, excludePatterns, (msg) => log(LogLevel.Debug, msg));
        } else {
            // Initialize minimal watchers (only .csproj)
            log(LogLevel.Info, 'Initializing minimal file watchers (waiting for generator marker)...');
            fileWatchers = createWatchers(['**/*.csproj'], undefined, [], (msg) => log(LogLevel.Debug, msg));
        }
        
        // Attach handlers to all watchers
        function attachWatcherHandlers() {
            for (const watcher of fileWatchers.values()) {
                watcher.onDidChange((uri) => forwardFileChange(uri, 2)); // Changed
                watcher.onDidCreate((uri) => forwardFileChange(uri, 1)); // Created
                watcher.onDidDelete((uri) => forwardFileChange(uri, 3)); // Deleted
            }
        };
        
        attachWatcherHandlers();
        context.subscriptions.push(...Array.from(fileWatchers.values()));

        // Commands - Register only if not already registered (prevents duplicate registration on retry)
        const existingCommands = await vscode.commands.getCommands(true);
        
        if (!existingCommands.includes(Constants.Commands.GENGORA_RUN)) {
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
        }

        if (!existingCommands.includes(Constants.Commands.GENGORA_SHOW_OUTPUT)) {
            context.subscriptions.push(vscode.commands.registerCommand(Constants.Commands.GENGORA_SHOW_OUTPUT, () => {
                output.show(false);
            }));
        }

        if (!existingCommands.includes(Constants.Commands.GENGORA_STOP)) {
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
        }

        // Status bar menu command
        if (!existingCommands.includes('gengora.statusBarMenu')) {
            context.subscriptions.push(vscode.commands.registerCommand('gengora.statusBarMenu', async () => {
                const currentLevel = config.get<string>('logLevel', Constants.Defaults.LOG_LEVEL);
                
                const items: vscode.QuickPickItem[] = [
                    { label: '$(play) Start', description: 'Start the generator' },
                    { label: '$(stop) Stop', description: 'Stop the generator' },
                    { label: '$(output) Show Output', description: 'Show output channel' },
                    { label: '', kind: vscode.QuickPickItemKind.Separator },
                    { label: `$(info) Log Level: ${currentLevel}`, description: 'Change logging verbosity' }
                ];

                const choice = await vscode.window.showQuickPick(items, { placeHolder: 'Gengora Menu' });
                
                if (!choice) return;

                if (choice.label.includes('Start')) {
                    await vscode.commands.executeCommand(Constants.Commands.GENGORA_RUN);
                } else if (choice.label.includes('Stop')) {
                    await vscode.commands.executeCommand(Constants.Commands.GENGORA_STOP);
                } else if (choice.label.includes('Show Output')) {
                    output.show(false);
                } else if (choice.label.includes('Log Level')) {
                    const levels = ['warning', 'info', 'debug'];
                    const levelChoice = await vscode.window.showQuickPick(levels, { 
                        placeHolder: `Current: ${currentLevel}` 
                    });
                    if (levelChoice && levelChoice !== currentLevel) {
                        await config.update('logLevel', levelChoice, vscode.ConfigurationTarget.Global);
                        vscode.window.showInformationMessage(`Log level changed to: ${levelChoice}`);
                    }
                }
            }));
        }

        // Auto-start if configured (NOTE: Server already auto-starts on initialization via OnInitialized)
        // This setting is kept for backward compatibility but server handles auto-start internally
        const autoRun = config.get<boolean>('autoRunOnCompileSuccess') ?? Constants.Defaults.AUTO_RUN_ON_COMPILE_SUCCESS;
        if (autoRun) {
            log(LogLevel.Debug, 'Note: gengora.autoRunOnCompileSuccess is deprecated - server handles auto-start internally');
        }

        log(LogLevel.Info, '=== Gengora Extension Activated ===');
        statusBar.text = Constants.StatusBar.READY;

    } catch (error: any) {
        isActivated = false; // Reset flag on error so retry can work
        const msg = `Activation failed: ${error?.message ?? error}`;
        log(LogLevel.Error, msg);
        vscode.window.showErrorMessage(`Gengora: ${msg}`);
        if (statusBar) {
            statusBar.text = Constants.StatusBar.ACTIVATION_FAILED;
        }
        throw error; // Re-throw to let VS Code know activation failed
    }
}

export function deactivate(): Promise<void> | undefined {
    if (!client) return undefined;
    isActivated = false; // Reset flag on deactivation
    log(LogLevel.Info, 'Deactivating...');
    return client.stop() as Promise<void>;
}
