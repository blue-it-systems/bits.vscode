namespace Gengora.Server.Core.Discovery;

/// <summary>
/// Represents A Discovered Generator Project.
/// </summary>
public sealed record GeneratorProjectInfo
{
    /// <summary>
    /// The Full Path To The .csproj File.
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// The Project Name (File Name Without Extension).
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// The Directory Containing The Project File.
    /// </summary>
    public required string ProjectDirectory { get; init; }

    /// <summary>
    /// Timestamp When The Project Was Discovered.
    /// </summary>
    public DateTimeOffset DiscoveredAt { get; init; } = DateTimeOffset.UtcNow;
}
