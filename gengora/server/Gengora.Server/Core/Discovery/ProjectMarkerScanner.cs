namespace Gengora.Server.Core.Discovery;

using Microsoft.Extensions.Logging;

/// <summary>
/// Scans Workspace For Generator Projects Marked With IsGeneratorProject.
/// Implements Specification R1.* Discovery Rules.
/// </summary>
public sealed class ProjectMarkerScanner
{
    private const string GENERATOR_MARKER = "<IsGeneratorProject>true</IsGeneratorProject>";
    private const string PROJECT_FILE_PATTERN = "*.csproj";

    private readonly ILogger<ProjectMarkerScanner> _Logger;

    public ProjectMarkerScanner(ILogger<ProjectMarkerScanner> logger)
    {
        this._Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Scans The Specified Directory Recursively For Generator Projects.
    /// Per R1.3: Discovery MUST Search Recursively From Workspace Root.
    /// </summary>
    /// <param name="workspaceRoot">The Root Directory To Scan.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>The First Discovered Generator Project, Or Null If None Found.</returns>
    public async Task<GeneratorProjectInfo?> ScanAsync
    (
        string workspaceRoot,
        CancellationToken cancellationToken = default
    )
    {
        if (String.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace Root Cannot Be Null Or Empty.", nameof(workspaceRoot));
        }

        if (!Directory.Exists(workspaceRoot))
        {
            this._Logger.LogWarning("Workspace Root Does Not Exist: {WorkspaceRoot}", workspaceRoot);

            return null;
        }

        this._Logger.LogDebug("Starting Generator Discovery In: {WorkspaceRoot}", workspaceRoot);

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

            this._Logger.LogDebug("No Generator Projects Found In: {WorkspaceRoot}", workspaceRoot);

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
            var containsMarker = content.Contains(GENERATOR_MARKER, StringComparison.OrdinalIgnoreCase);

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
