namespace Gengora.Server.Core.Execution;

using Gengora.Server.Core.Messaging;

/// <summary>
/// Result Of A Generator Execution.
/// </summary>
public sealed record ExecutionResult
{
    /// <summary>
    /// Whether Execution Completed Successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Exit Code From The Process.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Parsed Messages From Generator Output.
    /// </summary>
    public IReadOnlyList<GeneratorMessage> Messages { get; init; } = Array.Empty<GeneratorMessage>();

    /// <summary>
    /// Paths Of Emitted Files (Per R6.10).
    /// </summary>
    public IReadOnlyList<string> EmittedFiles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Duration Of Execution.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Error Message If Execution Failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Session ID Used For This Execution.
    /// </summary>
    public required string SessionId { get; init; }
}
