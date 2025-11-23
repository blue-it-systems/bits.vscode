namespace BITS.Gengora.Server.Models;

/// <summary>
/// Capabilities announced by the running generator via JSON handshake.
/// </summary>
public class GeneratorCapabilities
{
    public bool PublishDiagnostics { get; set; }
    public bool WatchMode { get; set; }
    public string[] WatchGlobs { get; set; } = [];
    public int WatchDebounceMs { get; set; } = Constants.Timeouts.DEFAULT_WATCH_DEBOUNCE_MS;
}
