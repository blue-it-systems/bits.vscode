using BITS.Gengora.Server.Models;

namespace BITS.Gengora.Server.Services;

/// <summary>
/// Service for managing generator compilation and execution lifecycle.
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
    /// Recompiles and restarts the generator (used for file watching).
    /// </summary>
    Task RestartGeneratorAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current generator capabilities.
    /// </summary>
    GeneratorCapabilities GetCapabilities();
}
