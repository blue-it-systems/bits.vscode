/**
 * Central location for all constant values used throughout the extension.
 * Keeps the codebase DRY and makes changes easier to manage.
 */

/**
 * LSP notification method names (must match server Constants.cs)
 */
export const Notifications = {
    GENERATOR_STATUS: '$/generator.status',
    GENERATOR_STDOUT: '$/generator.stdout',
    GENERATOR_STDERR: '$/generator.stderr',
    GENERATOR_ERROR: 'generator/error',
    GENERATOR_HELLO: 'generator/hello',
    OBSERVATION_MODE_CHANGED: 'generator/observationModeChanged',
    GENERATOR_PROJECT_DISCOVERED: 'generator/projectDiscovered',
} as const;

/**
 * LSP command identifiers (must match server Constants.cs)
 */
export const Commands = {
    GENGORA_START: 'gengora.start',
    GENGORA_STOP: 'gengora.stop',
    GENGORA_RUN: 'gengora.run',
    GENGORA_SHOW_OUTPUT: 'gengora.showOutput',
} as const;

/**
 * LSP method names
 */
export const Methods = {
    WORKSPACE_EXECUTE_COMMAND: 'workspace/executeCommand',
    WORKSPACE_DID_CHANGE_WATCHED_FILES: 'workspace/didChangeWatchedFiles',
} as const;

/**
 * Generator status states (must match server Constants.cs)
 */
export const States = {
    INITIALIZING: 'initializing',
    COMPILING: 'compiling',
    COMPILED: 'compiled',
    RUNNING: 'running',
    STOPPING: 'stopping',
    STOPPED: 'stopped',
    ERROR: 'error',
    WATCH_SKIPPED: 'watch-skipped',
    OBSERVING_MINIMAL: 'observing-minimal',
    OBSERVING_FULL: 'observing-full',
} as const;

/**
 * Build configuration
 */
export const Build = {
    TARGET_FRAMEWORK: 'net8.0',
    DEBUG_CONFIG: 'Debug',
    RELEASE_CONFIG: 'Release',
    SERVER_DLL_NAME: 'BITS.Gengora.Server.dll',
} as const;

/**
 * Default configuration values
 */
export const Defaults = {
    LOG_LEVEL: 'debug',
    AUTO_RUN_ON_COMPILE_SUCCESS: true,
    AUTO_START_DELAY_MS: 500,
    IGNORE_PATTERNS: [
        '**/bin/**',
        '**/obj/**',
        '**/.vscode/**',
        '**/GeneratedProject-*/**',
    ],
} as const;

/**
 * File change event types (LSP protocol)
 */
export const FileChangeType = {
    CREATED: 1,
    CHANGED: 2,
    DELETED: 3,
} as const;

/**
 * Log level prefixes for consistent formatting
 */
export const LogPrefixes = {
    ERROR: '[ERROR]',
    WARN: '[WARN]',
    INFO: '[INFO]',
    DEBUG: '[DEBUG]',
} as const;

/**
 * Status bar icons and text templates
 */
export const StatusBar = {
    INITIALIZING: '$(sync~spin) Gengora: initializing',
    READY: '$(check) Gengora: ready',
    COMPILING: '$(sync~spin) Gengora: compiling',
    COMPILED: '$(check) Gengora: compiled',
    RUNNING: '$(play) Gengora: running',
    ERROR: '$(error) Gengora: error',
    STOPPED: '$(debug-stop) Gengora: stopped',
    NO_WORKSPACE: '$(error) Gengora: No workspace',
    NO_GENERATOR: '$(error) Gengora: No generator project',
    SERVER_MISSING: '$(error) Gengora: Server missing',
    ACTIVATION_FAILED: '$(error) Gengora: activation failed',
    TOOLTIP: 'Gengora - Click to show output',
} as const;

/**
 * Configuration keys (settings in package.json)
 */
export const ConfigKeys = {
    SERVER_PATH: 'gengora.serverPath',
    GENERATOR_PROJECT_PATH: 'gengora.generatorProjectPath',
    EXCLUDE_PATTERNS: 'gengora.excludePatterns',
    AUTO_RUN_ON_COMPILE_SUCCESS: 'gengora.autoRunOnCompileSuccess',
    LOG_LEVEL: 'gengora.logLevel',
    MERGE_GITIGNORE: 'gengora.mergeGitignore',
    FILE_WATCH_IGNORE_PATTERNS: 'gengora.fileWatchIgnorePatterns',
} as const;

/**
 * File patterns for watchers
 */
export const FilePatterns = {
    CSHARP: '**/*.cs',
    CSPROJ: '**/*.csproj',
} as const;

/**
 * CLI arguments for server startup
 */
export const CliArgs = {
    WORKSPACE_ROOT: '--workspace-root',
} as const;
