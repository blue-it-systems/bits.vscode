namespace Gengora.Server.Lsp;

using System.Text.Json;
using System.Text.Json.Serialization;
using Gengora.Server.Core.StateMachine;

/// <summary>
/// LSP Request And Response Types For Gengora Protocol.
/// </summary>

// Initialize Request - Full LSP Spec
public sealed record InitializeParams
{
    /// <summary>
    /// The Process Id Of The Parent Process That Started The Server.
    /// </summary>
    [JsonPropertyName("processId")]
    public int? ProcessId { get; init; }

    /// <summary>
    /// Information About The Client.
    /// </summary>
    [JsonPropertyName("clientInfo")]
    public ClientInfo? ClientInfo { get; init; }

    /// <summary>
    /// The Locale The Client Is Currently Showing The User Interface In.
    /// </summary>
    [JsonPropertyName("locale")]
    public string? Locale { get; init; }

    /// <summary>
    /// The RootPath Of The Workspace (Deprecated, Use RootUri).
    /// </summary>
    [JsonPropertyName("rootPath")]
    public string? RootPath { get; init; }

    /// <summary>
    /// The RootUri Of The Workspace.
    /// </summary>
    [JsonPropertyName("rootUri")]
    public string? RootUri { get; init; }

    /// <summary>
    /// User Provided Initialization Options.
    /// </summary>
    [JsonPropertyName("initializationOptions")]
    public JsonElement? InitializationOptions { get; init; }

    /// <summary>
    /// The Capabilities Provided By The Client.
    /// </summary>
    [JsonPropertyName("capabilities")]
    public ClientCapabilities? Capabilities { get; init; }

    /// <summary>
    /// The Initial Trace Setting.
    /// </summary>
    [JsonPropertyName("trace")]
    public string? Trace { get; init; }

    /// <summary>
    /// The Workspace Folders Configured In The Client.
    /// </summary>
    [JsonPropertyName("workspaceFolders")]
    public IReadOnlyList<WorkspaceFolder>? WorkspaceFolders { get; init; }

    /// <summary>
    /// Gets The Effective Root Path From Either RootUri Or RootPath.
    /// </summary>
    [JsonIgnore]
    public string? EffectiveRootPath
    {
        get
        {
            if (!String.IsNullOrEmpty(this.RootUri))
            {
                // Convert file:// URI To Path
                if (Uri.TryCreate(this.RootUri, UriKind.Absolute, out var uri))
                {
                    return uri.LocalPath;
                }
            }

            return this.RootPath;
        }
    }
}

public sealed record ClientInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }
}

public sealed record WorkspaceFolder
{
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

public sealed record ClientCapabilities
{
    [JsonPropertyName("workspace")]
    public WorkspaceCapabilities? Workspace { get; init; }

    [JsonPropertyName("textDocument")]
    public JsonElement? TextDocument { get; init; }

    [JsonPropertyName("window")]
    public JsonElement? Window { get; init; }

    [JsonPropertyName("general")]
    public JsonElement? General { get; init; }

    [JsonPropertyName("experimental")]
    public JsonElement? Experimental { get; init; }
}

public sealed record WorkspaceCapabilities
{
    [JsonPropertyName("workspaceFolders")]
    public bool? WorkspaceFolders { get; init; }

    [JsonPropertyName("configuration")]
    public bool? Configuration { get; init; }
}

public sealed record InitializeResult
{
    [JsonPropertyName("serverInfo")]
    public required ServerInfo ServerInfo { get; init; }

    [JsonPropertyName("capabilities")]
    public required ServerCapabilities Capabilities { get; init; }
}

public sealed record ServerInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }
}

public sealed record ServerCapabilities
{
    [JsonPropertyName("stateNotifications")]
    public bool StateNotifications { get; init; } = true;

    [JsonPropertyName("diagnostics")]
    public bool Diagnostics { get; init; } = true;

    [JsonPropertyName("fileWatching")]
    public bool FileWatching { get; init; } = true;
}

// State Notification
public sealed record StateChangedNotification
{
    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("previousState")]
    public required string PreviousState { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }
}

// Diagnostic Notification
public sealed record DiagnosticsNotification
{
    [JsonPropertyName("diagnostics")]
    public required IReadOnlyList<LspDiagnostic> Diagnostics { get; init; }

    [JsonPropertyName("isCompilationError")]
    public bool IsCompilationError { get; init; }
}

public sealed record LspDiagnostic
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }

    [JsonPropertyName("line")]
    public int? Line { get; init; }

    [JsonPropertyName("column")]
    public int? Column { get; init; }
}

// File Emitted Notification
public sealed record FileEmittedNotification
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }
}

// Get State Request/Response
public sealed record GetStateResult
{
    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("project")]
    public ProjectInfoResult? Project { get; init; }
}

public sealed record ProjectInfoResult
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("directory")]
    public required string Directory { get; init; }
}

// Recompile Request
public sealed record RecompileResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
