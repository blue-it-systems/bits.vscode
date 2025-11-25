namespace Gengora.Server.Core.FileWatching;

using Gengora.Server.Core.Discovery;
using Microsoft.Extensions.Logging;

/// <summary>
/// Watches Generator Project Files For Changes.
/// Implements Specification R3.* Hot-Reload Compilation Workflow.
/// </summary>
public sealed class FileWatcherService : IDisposable
{
    private readonly ILogger<FileWatcherService> _Logger;
    private readonly IgnorePatternMatcher _IgnorePatternMatcher;
    private FileSystemWatcher? _Watcher;
    private FileSystemWatcher? _WorkspaceWatcher;
    private GeneratorProjectInfo? _CurrentProject;
    private EventHandler<FileChangedEventArgs>? _WorkspaceFileChangedHandler;
    private bool _IsDisposed;

    /// <summary>
    /// Event Raised When A Relevant Source File Changes.
    /// </summary>
    public event EventHandler<FileChangedEventArgs>? FileChanged;

    public FileWatcherService
    (
        ILogger<FileWatcherService> logger,
        IgnorePatternMatcher ignorePatternMatcher
    )
    {
        this._Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this._IgnorePatternMatcher = ignorePatternMatcher ?? throw new ArgumentNullException(nameof(ignorePatternMatcher));
    }

    /// <summary>
    /// Gets Whether The File Watcher Is Currently Active.
    /// </summary>
    public bool IsWatching => this._Watcher?.EnableRaisingEvents ?? false;

    /// <summary>
    /// Gets Whether The Workspace Watcher Is Currently Active.
    /// </summary>
    public bool IsWatchingWorkspace => this._WorkspaceWatcher?.EnableRaisingEvents ?? false;

    /// <summary>
    /// Starts Watching The Workspace Directory For .csproj Files.
    /// This Is Used When No Generator Project Is Currently Active To Detect When One Becomes Available.
    /// </summary>
    public void StartWatchingWorkspace(string workspaceRoot, EventHandler<FileChangedEventArgs> handler)
    {
        this.ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentNullException(nameof(workspaceRoot));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        this.StopWatchingWorkspace();

        this._WorkspaceFileChangedHandler = handler;

        try
        {
            this._WorkspaceWatcher = new FileSystemWatcher(workspaceRoot)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                Filter = "*.csproj",
                IncludeSubdirectories = true,
                EnableRaisingEvents = false
            };

            this._WorkspaceWatcher.Changed += this.OnWorkspaceFileSystemEvent;
            this._WorkspaceWatcher.Created += this.OnWorkspaceFileSystemEvent;
            this._WorkspaceWatcher.Error += this.OnWatcherError;

            this._WorkspaceWatcher.EnableRaisingEvents = true;

            this._Logger.LogInformation
            (
                "Started Watching Workspace: {WorkspaceRoot} For .csproj File Changes",
                workspaceRoot
            );
        }
        catch (Exception ex)
        {
            this._Logger.LogError(ex, "Failed To Start Workspace Watcher For: {WorkspaceRoot}", workspaceRoot);

            throw;
        }
    }

    /// <summary>
    /// Stops Watching The Workspace Directory.
    /// </summary>
    public void StopWatchingWorkspace()
    {
        if (this._WorkspaceWatcher != null)
        {
            this._WorkspaceWatcher.EnableRaisingEvents = false;
            this._WorkspaceWatcher.Changed -= this.OnWorkspaceFileSystemEvent;
            this._WorkspaceWatcher.Created -= this.OnWorkspaceFileSystemEvent;
            this._WorkspaceWatcher.Error -= this.OnWatcherError;
            this._WorkspaceWatcher.Dispose();
            this._WorkspaceWatcher = null;

            this._Logger.LogInformation("Stopped Workspace Watcher");
        }

        this._WorkspaceFileChangedHandler = null;
    }

    private void OnWorkspaceFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        // Only Process .csproj Files For Workspace Watching
        if (e.FullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            this._Logger.LogDebug
            (
                "Workspace .csproj File Changed: {FilePath} ({ChangeType})",
                e.FullPath,
                e.ChangeType
            );

            this._WorkspaceFileChangedHandler?.Invoke(this, new FileChangedEventArgs(e.FullPath, e.ChangeType));
        }
    }

    /// <summary>
    /// Starts Watching The Specified Generator Project Directory.
    /// </summary>
    public void StartWatching(GeneratorProjectInfo project)
    {
        this.ThrowIfDisposed();

        if (project == null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        this.StopWatching();

        this._CurrentProject = project;

        try
        {
            this._Watcher = new FileSystemWatcher(project.ProjectDirectory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = true,
                EnableRaisingEvents = false
            };

            this._Watcher.Changed += this.OnFileSystemEvent;
            this._Watcher.Created += this.OnFileSystemEvent;
            this._Watcher.Renamed += this.OnFileSystemRenamed;
            this._Watcher.Error += this.OnWatcherError;

            this._Watcher.EnableRaisingEvents = true;

            this._Logger.LogInformation
            (
                "Started Watching: {ProjectDirectory} For Project: {ProjectName}",
                project.ProjectDirectory,
                project.ProjectName
            );
        }
        catch (Exception ex)
        {
            this._Logger.LogError(ex, "Failed To Start File Watcher For: {ProjectDirectory}", project.ProjectDirectory);

            throw;
        }
    }

    /// <summary>
    /// Stops Watching For File Changes.
    /// </summary>
    public void StopWatching()
    {
        if (this._Watcher != null)
        {
            this._Watcher.EnableRaisingEvents = false;
            this._Watcher.Changed -= this.OnFileSystemEvent;
            this._Watcher.Created -= this.OnFileSystemEvent;
            this._Watcher.Renamed -= this.OnFileSystemRenamed;
            this._Watcher.Error -= this.OnWatcherError;
            this._Watcher.Dispose();
            this._Watcher = null;

            this._Logger.LogInformation("Stopped File Watcher");
        }

        this._CurrentProject = null;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        this.ProcessFileChange(e.FullPath, e.ChangeType);
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        this.ProcessFileChange(e.FullPath, e.ChangeType);
    }

    private void ProcessFileChange(string filePath, WatcherChangeTypes changeType)
    {
        // R5.5: Test Path Against All Patterns
        if (this._IgnorePatternMatcher.ShouldIgnore(filePath))
        {
            this._Logger.LogDebug("Ignoring Change To: {FilePath}", filePath);

            return;
        }

        // Only Process Source Files
        if (!this.IsSourceFile(filePath))
        {
            this._Logger.LogDebug("Ignoring Non-Source File: {FilePath}", filePath);

            return;
        }

        this._Logger.LogDebug
        (
            "Source File Changed: {FilePath} ({ChangeType})",
            filePath,
            changeType
        );

        // R5.7: Trigger Recompilation Workflow
        this.FileChanged?.Invoke(this, new FileChangedEventArgs(filePath, changeType));
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();

        this._Logger.LogError(ex, "File Watcher Error");

        // R8.4: File Watcher Errors MUST NOT Prevent Extension Startup
        // Log And Continue In Degraded Mode
    }

    private bool IsSourceFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".props", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".targets", StringComparison.OrdinalIgnoreCase);
    }

    private void ThrowIfDisposed()
    {
        if (this._IsDisposed)
        {
            throw new ObjectDisposedException(nameof(FileWatcherService));
        }
    }

    public void Dispose()
    {
        if (!this._IsDisposed)
        {
            this.StopWatching();
            this.StopWatchingWorkspace();
            this._IsDisposed = true;
        }
    }
}

/// <summary>
/// Event Arguments For File Change Events.
/// </summary>
public sealed class FileChangedEventArgs : EventArgs
{
    public string FilePath { get; }
    public WatcherChangeTypes ChangeType { get; }
    public DateTimeOffset Timestamp { get; }

    public FileChangedEventArgs(string filePath, WatcherChangeTypes changeType)
    {
        this.FilePath = filePath;
        this.ChangeType = changeType;
        this.Timestamp = DateTimeOffset.UtcNow;
    }
}
