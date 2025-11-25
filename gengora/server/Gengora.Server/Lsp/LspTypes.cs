namespace Gengora.Server.Lsp;

using System.Text.Json.Serialization;
using Gengora.Server.Core.StateMachine;

/// <summary>
/// LSP Request And Response Types For Gengora Protocol.
/// </summary>
/// 
// Initialize Request
public sealed record InitializeParams
{
    [JsonPropertyName("rootPath")]
    public required string RootPath { get; init; }

    [JsonPropertyName("capabilities")]
    public ClientCapabilities? Capabilities { get; init; }
}

public sealed record ClientCapabilities
{
    [JsonPropertyName("statusBar")]
    public bool StatusBar { get; init; } = true;

    [JsonPropertyName("diagnostics")]
    public bool Diagnostics { get; init; } = true;
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
