namespace Gengora.Server.Core.Discovery;

using Microsoft.Extensions.Logging;

/// <summary>
/// Scans Workspace For Generator Projects Marked With IsGeneratorProject.
/// Implements Specification R1.* Discovery Rules.
/// </summary>
public sealed class ProjectMarkerScanner
{
    private const string GENERATOR_MARKER_TRUE = "<IsGeneratorProject>true</IsGeneratorProject>";
    private const string PROJECT_FILE_PATTERN = "*.csproj";

    private readonly ILogger<ProjectMarkerScanner> _Logger;

    public ProjectMarkerScanner(ILogger<ProjectMarkerScanner> logger)
    {
        this._Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks If A Specific Project File Still Has The Generator Marker Set To True.
    /// Used To Detect When The Marker Is Removed Or Set To False.
    /// </summary>
    /// <param name="projectPath">The Path To The Project File.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>True If The Project Is Still A Generator Project.</returns>
    public async Task<bool> IsStillGeneratorProjectAsync
    (
        string projectPath,
        CancellationToken cancellationToken = default
    )
    {
        if (String.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
        {
            return false;
        }

        return await this.ContainsGeneratorMarkerAsync(projectPath, cancellationToken);
    }

    /// <summary>
    /// Scans Multiple Workspace Roots Recursively For Generator Projects.
    /// Per R1.3: Discovery MUST Search Recursively From All Workspace Roots.
    /// </summary>
    /// <param name="workspaceRoots">The Root Directories To Scan.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>The First Discovered Generator Project, Or Null If None Found.</returns>
    public async Task<GeneratorProjectInfo?> ScanAsync
    (
        IEnumerable<string> workspaceRoots,
        CancellationToken cancellationToken = default
    )
    {
        if (workspaceRoots == null)
        {
            throw new ArgumentNullException(nameof(workspaceRoots));
        }

        var roots = workspaceRoots.ToList();

        if (roots.Count == 0)
        {
            throw new ArgumentException("At Least One Workspace Root Must Be Provided.", nameof(workspaceRoots));
        }

        this._Logger.LogDebug("Starting Generator Discovery In {Count} Workspace Root(s)", roots.Count);

        foreach (var workspaceRoot in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (String.IsNullOrWhiteSpace(workspaceRoot))
            {
                this._Logger.LogWarning("Skipping Empty Workspace Root");

                continue;
            }

            if (!Directory.Exists(workspaceRoot))
            {
                this._Logger.LogWarning("Workspace Root Does Not Exist: {WorkspaceRoot}", workspaceRoot);

                continue;
            }

            this._Logger.LogDebug("Scanning Workspace Root: {WorkspaceRoot}", workspaceRoot);

            var project = await this.ScanSingleRootAsync(workspaceRoot, cancellationToken);

            if (project != null)
            {
                return project;
            }
        }

        this._Logger.LogDebug("No Generator Projects Found In Any Workspace Root");

        return null;
    }

    /// <summary>
    /// Scans A Single Workspace Root Recursively For Generator Projects.
    /// </summary>
    private async Task<GeneratorProjectInfo?> ScanSingleRootAsync
    (
        string workspaceRoot,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var projectFiles = Directory.EnumerateFiles
            (
                workspaceRoot,
                PROJECT_FILE_PATTERN,
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    MatchCasing = MatchCasing.CaseInsensitive
                }
            );

            foreach (var projectPath in projectFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip Common Non-Source Directories
                if (this.ShouldSkipPath(projectPath))
                {
                    continue;
                }

                if (await this.ContainsGeneratorMarkerAsync(projectPath, cancellationToken))
                {
                    var projectInfo = new GeneratorProjectInfo
                    {
                        ProjectPath = projectPath,
                        ProjectName = Path.GetFileNameWithoutExtension(projectPath),
                        ProjectDirectory = Path.GetDirectoryName(projectPath)!
                    };

                    this._Logger.LogInformation
                    (
                        "Generator Project Discovered: {ProjectName} At {ProjectPath}",
                        projectInfo.ProjectName,
                        projectInfo.ProjectPath
                    );

                    return projectInfo;
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            this._Logger.LogDebug("Discovery Scan Cancelled");

            throw;
        }
        catch (Exception ex)
        {
            this._Logger.LogError(ex, "Error During Generator Discovery In: {WorkspaceRoot}", workspaceRoot);

            return null;
        }
    }

    /// <summary>
    /// Checks If The Project File Contains The Generator Marker.
    /// Per R1.1: A Generator Project Is Identified By IsGeneratorProject Marker.
    /// </summary>
    private async Task<bool> ContainsGeneratorMarkerAsync
    (
        string projectPath,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var content = await File.ReadAllTextAsync(projectPath, cancellationToken);

            // Case-Insensitive Search For Marker
            var containsMarker = content.Contains(GENERATOR_MARKER_TRUE, StringComparison.OrdinalIgnoreCase);

            if (containsMarker)
            {
                this._Logger.LogDebug("Found Generator Marker In: {ProjectPath}", projectPath);
            }

            return containsMarker;
        }
        catch (Exception ex)
        {
            this._Logger.LogWarning(ex, "Failed To Read Project File: {ProjectPath}", projectPath);

            return false;
        }
    }

    /// <summary>
    /// Determines If A Path Should Be Skipped During Scanning.
    /// Excludes Common Build Artifacts And Dependency Directories.
    /// </summary>
    private bool ShouldSkipPath(string path)
    {
        var skipPatterns = new[]
        {
            Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + ".vs" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + "packages" + Path.DirectorySeparatorChar
        };

        foreach (var pattern in skipPatterns)
        {
            if (path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
