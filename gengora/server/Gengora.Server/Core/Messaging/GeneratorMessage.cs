namespace Gengora.Server.Core.Messaging;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Represents A Structured Message From A Generator Process.
/// Implements Specification R6.* Generator Interface Contract.
/// </summary>
public sealed record GeneratorMessage
{
    /// <summary>
    /// Message Type (e.g., "generator/status", "generator/file").
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Action Being Performed (e.g., "start", "emit", "complete").
    /// </summary>
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    /// <summary>
    /// Human-Readable Message Content.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Session ID For Correlation (R6.5).
    /// </summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// ISO 8601 Timestamp.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

    /// <summary>
    /// Optional File Path For File Events.
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    /// <summary>
    /// Determines If This Is A Status Message.
    /// </summary>
    [JsonIgnore]
    public bool IsStatusMessage => this.Type == "generator/status";

    /// <summary>
    /// Determines If This Is A File Emit Message.
    /// </summary>
    [JsonIgnore]
    public bool IsFileEmitMessage => this.Type == "generator/file" && this.Action == "emit";
}

/// <summary>
/// Common Message Types.
/// </summary>
public static class MessageTypes
{
    public const string STATUS = "generator/status";
    public const string FILE = "generator/file";
}

/// <summary>
/// Common Message Actions.
/// </summary>
public static class MessageActions
{
    public const string START = "start";
    public const string ANALYZING = "analyzing";
    public const string EMIT = "emit";
    public const string COMPLETE = "complete";
    public const string ERROR = "error";
}
