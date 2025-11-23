namespace BITS.Gengora.Server.Services;

/// <summary>
/// Manages observation state and file watching behavior based on generator project marker.
/// Handles dynamic upgrade/downgrade of observation scope.
/// </summary>
public class ObservationManager
{
    private readonly string _WorkspaceRoot;
    private string? _CurrentProjectPath;
    private ObservationMode _CurrentMode;

    public ObservationMode CurrentMode => this._CurrentMode;
    public string? CurrentProjectPath => this._CurrentProjectPath;
    public string? CurrentProjectFolder => this._CurrentProjectPath != null 
        ? Path.GetDirectoryName(this._CurrentProjectPath) 
        : null;

    public event Action<ObservationMode, ObservationMode>? OnModeChanged;

    public ObservationManager(string workspaceRoot)
    {
        this._WorkspaceRoot = workspaceRoot;
        this._CurrentMode = ObservationMode.GlobalScan;
    }

    /// <summary>
    /// Sets the generator project and determines observation mode based on marker.
    /// </summary>
    public async Task<bool> SetGeneratorProjectAsync(string? csprojPath, CancellationToken ct)
    {
        var oldMode = this._CurrentMode;
        
        if (string.IsNullOrEmpty(csprojPath) || !File.Exists(csprojPath))
        {
            this._CurrentProjectPath = null;
            this._CurrentMode = ObservationMode.GlobalScan;
            this.NotifyModeChange(oldMode, this._CurrentMode);
            return false;
        }

        this._CurrentProjectPath = csprojPath;
        
        // Check for marker
        var hasMarker = await this.CheckMarkerAsync(csprojPath, ct);
        this._CurrentMode = hasMarker ? ObservationMode.FullObservation : ObservationMode.MinimalObservation;
        
        this.NotifyModeChange(oldMode, this._CurrentMode);
        return true;
    }

    /// <summary>
    /// Re-checks the project marker and updates observation mode if changed.
    /// Returns true if mode changed.
    /// </summary>
    public async Task<bool> RecheckMarkerAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(this._CurrentProjectPath))
        {
            return false;
        }

        var hasMarker = await this.CheckMarkerAsync(this._CurrentProjectPath, ct);
        var newMode = hasMarker ? ObservationMode.FullObservation : ObservationMode.MinimalObservation;
        
        if (newMode != this._CurrentMode)
        {
            var oldMode = this._CurrentMode;
            this._CurrentMode = newMode;
            this.NotifyModeChange(oldMode, newMode);
            return true;
        }

        return false;
    }

    private async Task<bool> CheckMarkerAsync(string csprojPath, CancellationToken ct)
    {
        try
        {
            var content = await File.ReadAllTextAsync(csprojPath, ct);
            return content.Contains(Constants.Patterns.GENERATOR_PROJECT_MARKER, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void NotifyModeChange(ObservationMode oldMode, ObservationMode newMode)
    {
        if (oldMode != newMode)
        {
            this.OnModeChanged?.Invoke(oldMode, newMode);
        }
    }

    /// <summary>
    /// Gets file watch patterns based on current observation mode.
    /// </summary>
    public string[] GetWatchPatterns()
    {
        return this._CurrentMode switch
        {
            ObservationMode.FullObservation => 
            [
                "**/*" + Constants.Patterns.CSHARP_EXTENSION,
                "**/*" + Constants.Patterns.CSPROJ_EXTENSION
            ],
            ObservationMode.MinimalObservation when this._CurrentProjectPath != null =>
            [
                Path.GetFileName(this._CurrentProjectPath)
            ],
            ObservationMode.GlobalScan =>
            [
                "**/*" + Constants.Patterns.CSPROJ_EXTENSION
            ],
            _ => []
        };
    }

    /// <summary>
    /// Gets the base path for file watching.
    /// </summary>
    public string GetWatchBasePath()
    {
        return this._CurrentMode switch
        {
            ObservationMode.FullObservation => this.CurrentProjectFolder ?? this._WorkspaceRoot,
            ObservationMode.MinimalObservation => this.CurrentProjectFolder ?? this._WorkspaceRoot,
            ObservationMode.GlobalScan => this._WorkspaceRoot,
            _ => this._WorkspaceRoot
        };
    }
}

/// <summary>
/// Observation modes for generator project monitoring.
/// </summary>
public enum ObservationMode
{
    /// <summary>
    /// Scanning entire workspace for .csproj files with marker.
    /// </summary>
    GlobalScan,

    /// <summary>
    /// Observing only the .csproj file for marker changes.
    /// </summary>
    MinimalObservation,

    /// <summary>
    /// Observing entire generator project folder for all changes.
    /// </summary>
    FullObservation
}
