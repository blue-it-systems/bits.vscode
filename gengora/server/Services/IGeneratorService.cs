using BITS.Gengora.Server.Models;

namespace BITS.Gengora.Server.Services;

/// <summary>
/// Service for managing generator compilation and execution lifecycle with dynamic observation.
/// </summary>
public interface IGeneratorService
{
    /// <summary>
    /// Starts the generator: compile, emit, and execute.
    /// </summary>
    Task StartGeneratorAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the running generator process.
    /// </summary>
    Task StopGeneratorAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Pauses the generator (stops execution but keeps observation active).
    /// </summary>
    Task PauseGeneratorAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Recompiles and restarts the generator (used for file watching).
    /// </summary>
    Task RestartGeneratorAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Switches to a different generator project and restarts observation.
    /// </summary>
    Task SwitchProjectAsync(string projectPath, CancellationToken cancellationToken);

    /// <summary>
    /// Handles file change events (checks if .csproj marker changed).
    /// </summary>
    Task HandleFileChangeAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current generator capabilities.
    /// </summary>
    GeneratorCapabilities GetCapabilities();
}
