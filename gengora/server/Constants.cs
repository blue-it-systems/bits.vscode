namespace BITS.Gengora.Server;

/// <summary>
/// Central location for all constant values used throughout the server.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// LSP protocol constants
    /// </summary>
    public static class Lsp
    {
        public const string JSONRPC_VERSION = "2.0";
        public const string HEADER_CONTENT_LENGTH = "Content-Length";
        public const int DEFAULT_BUFFER_SIZE = 2048;
    }

    /// <summary>
    /// LSP method names
    /// </summary>
    public static class Methods
    {
        public const string INITIALIZE = "initialize";
        public const string INITIALIZED = "initialized";
        public const string SHUTDOWN = "shutdown";
        public const string EXIT = "exit";
        public const string WORKSPACE_EXECUTE_COMMAND = "workspace/executeCommand";
        public const string WORKSPACE_DID_CHANGE_WATCHED_FILES = "workspace/didChangeWatchedFiles";
        public const string TEXT_DOCUMENT_PUBLISH_DIAGNOSTICS = "textDocument/publishDiagnostics";
    }

    /// <summary>
    /// Custom notification methods
    /// </summary>
    public static class Notifications
    {
        public const string GENERATOR_STATUS = "$/generator.status";
        public const string GENERATOR_STDOUT = "$/generator.stdout";
        public const string GENERATOR_STDERR = "$/generator.stderr";
        public const string GENERATOR_HELLO = "generator/hello";
        public const string GENERATOR_PUBLISH_DIAGNOSTICS = "generator/publishDiagnostics";
        public const string GENERATOR_PUBLISH_DIAGNOSTICS_ALT = "generator.publishDiagnostics";
    }

    /// <summary>
    /// Command identifiers
    /// </summary>
    public static class Commands
    {
        // Legacy command names
        public const string GENERATOR_START = "generator.start";
        public const string GENERATOR_STOP = "generator.stop";
        
        // New command names
        public const string GENGORA_START = "gengora.start";
        public const string GENGORA_STOP = "gengora.stop";

        /// <summary>
        /// All supported commands for capability advertisement
        /// </summary>
        public static readonly string[] ALL_COMMANDS = 
        [
            GENERATOR_START,
            GENERATOR_STOP,
            GENGORA_START,
            GENGORA_STOP
        ];
    }

    /// <summary>
    /// Generator status states
    /// </summary>
    public static class States
    {
        public const string INITIALIZING = "initializing";
        public const string COMPILING = "compiling";
        public const string COMPILED = "compiled";
        public const string RUNNING = "running";
        public const string STOPPING = "stopping";
        public const string STOPPED = "stopped";
        public const string ERROR = "error";
        public const string WATCH_SKIPPED = "watch-skipped";
    }

    /// <summary>
    /// Error messages
    /// </summary>
    public static class ErrorMessages
    {
        public const string PROJECT_NOT_FOUND = "Generator project not found";
        public const string COMPILATION_FAILED = "Compilation failed";
        public const string EMIT_FAILED = "Emit failed";
        public const string PROCESS_ALREADY_RUNNING = "Process already running";
        public const string GENERATOR_PROJECT_NOT_LOADED = "Generator project not loaded";
        public const string WATCH_MODE_SKIPPED = "Generator manages its own watch-mode; coordinator will not recompile automatically.";
    }

    /// <summary>
    /// Build and compilation constants
    /// </summary>
    public static class Build
    {
        public const string DOTNET_COMMAND = "dotnet";
        public const string BUILD_ARGS_TEMPLATE = "build \"{0}\" --no-restore --nologo";
        public const string TARGET_FRAMEWORK = "net8.0";
        public const string DEBUG_CONFIG = "Debug";
        public const string BIN_FOLDER = "bin";
        public const string DLL_EXTENSION = ".dll";
        public const string PDB_EXTENSION = ".pdb";
        public const string RUNTIME_CONFIG_EXTENSION = ".runtimeconfig.json";
        public const string DEPS_EXTENSION = ".deps.json";
    }

    /// <summary>
    /// File and directory patterns
    /// </summary>
    public static class Patterns
    {
        public const string GENGORA_FOLDER_NAME = "Gengora";
        public const string CSPROJ_PATTERN = "*.csproj";
    }

    /// <summary>
    /// Diagnostic severity mapping
    /// </summary>
    public static class DiagnosticSeverity
    {
        public const string ERROR = "Error";
        public const string WARNING = "Warning";
        public const string INFO = "Info";
        
        public const int LSP_ERROR = 1;
        public const int LSP_WARNING = 2;
        public const int LSP_INFORMATION = 3;
        public const int LSP_HINT = 4;

        public static int ToLspSeverity(string severity)
        {
            return severity switch
            {
                ERROR => LSP_ERROR,
                WARNING => LSP_WARNING,
                _ => LSP_INFORMATION
            };
        }
    }

    /// <summary>
    /// Timeouts and delays
    /// </summary>
    public static class Timeouts
    {
        public const int GRACEFUL_SHUTDOWN_SECONDS = 2;
        public const int WATCH_DEBOUNCE_MS = 500;
        public const int MAIN_LOOP_DELAY_MS = 10;
        public const int PROCESS_CHECK_DELAY_MS = 200;
        public const int DEFAULT_WATCH_DEBOUNCE_MS = 400;
    }

    /// <summary>
    /// Environment variables
    /// </summary>
    public static class Environment
    {
        public const string GENERATOR_PROJECT_PATH = "GENERATOR_PROJECT_PATH";
        public const string GENERATOR_FOLDER_PATH = "GENERATOR_FOLDER_PATH";
    }

    /// <summary>
    /// Output directories
    /// </summary>
    public static class Directories
    {
        public const string VSCODE_FOLDER = ".vscode";
        public const string GENERATOR_FOLDER = ".generator";
        public const string OUT_FOLDER = "out";
    }

    /// <summary>
    /// CLI arguments
    /// </summary>
    public static class CliArgs
    {
        public const string WORKSPACE_ROOT = "--workspace-root";
    }
}
