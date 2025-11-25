namespace Gengora.Server.Core.Compilation;

/// <summary>
/// Result Of A Compilation Operation.
/// </summary>
public sealed record CompilationResult
{
    /// <summary>
    /// Whether Compilation Succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Path To Compiled Assembly (If Success Is True).
    /// </summary>
    public string? AssemblyPath { get; init; }

    /// <summary>
    /// Compilation Diagnostics (Errors And Warnings).
    /// </summary>
    public IReadOnlyList<CompilationDiagnostic> Diagnostics { get; init; } = Array.Empty<CompilationDiagnostic>();

    /// <summary>
    /// Duration Of Compilation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Error Message If Compilation Failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents A Single Compilation Diagnostic.
/// </summary>
public sealed record CompilationDiagnostic
{
    /// <summary>
    /// Diagnostic ID (e.g., "CS0001").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-Readable Message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Severity Level.
    /// </summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// File Path Where Diagnostic Was Reported.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Line Number (1-Based).
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Column Number (1-Based).
    /// </summary>
    public int? Column { get; init; }
}

/// <summary>
/// Diagnostic Severity Levels.
/// </summary>
public enum DiagnosticSeverity
{
    Hidden = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}
