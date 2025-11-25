namespace Gengora.Server.Core.FileWatching;

using Microsoft.Extensions.Logging;

/// <summary>
/// Matches File Paths Against Ignore Patterns.
/// Implements Specification R5.* File Change Detection Filtering.
/// </summary>
public sealed class IgnorePatternMatcher
{
    private readonly ILogger<IgnorePatternMatcher> _Logger;
    private readonly HashSet<string> _Patterns;

    /// <summary>
    /// Default Patterns To Ignore (R5.2).
    /// </summary>
    private static readonly string[] DEFAULT_PATTERNS =
    [
        // Build Outputs
        "**/bin/**",
        "**/obj/**",
        "**/out/**",
        "**/dist/**",

        // Package Managers
        "**/node_modules/**",
        "**/packages/**",

        // Version Control
        "**/.git/**",
        "**/.svn/**",

        // IDE Artifacts
        "**/.vs/**",
        "**/.vscode/.generator/**",
        "**/.idea/**",

        // Generated Outputs
        "**/gengora-output/**",

        // Dependency Caches
        "**/.nuget/**",
        "**/TestResults/**"
    ];

    public IgnorePatternMatcher(ILogger<IgnorePatternMatcher> logger)
    {
        this._Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this._Patterns = new HashSet<string>(DEFAULT_PATTERNS, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets The Current Set Of Ignore Patterns.
    /// </summary>
    public IReadOnlyCollection<string> Patterns => this._Patterns;

    /// <summary>
    /// Adds Custom Patterns To The Matcher (R5.8, R5.9).
    /// </summary>
    public void AddPatterns(IEnumerable<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (!String.IsNullOrWhiteSpace(pattern))
            {
                this._Patterns.Add(pattern);
                this._Logger.LogDebug("Added Ignore Pattern: {Pattern}", pattern);
            }
        }
    }

    /// <summary>
    /// Loads And Merges Patterns From A .gitignore File (R5.3, R5.4).
    /// </summary>
    public async Task LoadGitignoreAsync(string gitignorePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(gitignorePath))
        {
            this._Logger.LogDebug("Gitignore File Not Found: {Path}", gitignorePath);

            return;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(gitignorePath, cancellationToken);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Skip Empty Lines And Comments
                if (String.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                {
                    continue;
                }

                // Convert Gitignore Pattern To Glob Pattern
                var pattern = this.ConvertGitignoreToGlob(trimmed);
                this._Patterns.Add(pattern);
            }

            this._Logger.LogDebug
            (
                "Loaded {Count} Patterns From Gitignore: {Path}",
                lines.Length,
                gitignorePath
            );
        }
        catch (Exception ex)
        {
            this._Logger.LogWarning(ex, "Failed To Load Gitignore: {Path}", gitignorePath);
        }
    }

    /// <summary>
    /// Determines If A Path Should Be Ignored Based On Patterns (R5.5, R5.6).
    /// </summary>
    public bool ShouldIgnore(string filePath)
    {
        if (String.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        // Normalize Path Separators
        var normalizedPath = filePath.Replace('\\', '/');

        foreach (var pattern in this._Patterns)
        {
            if (this.MatchesPattern(normalizedPath, pattern))
            {
                this._Logger.LogDebug("Path Matches Ignore Pattern: {Path} -> {Pattern}", filePath, pattern);

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts A Gitignore Pattern To A Glob Pattern.
    /// </summary>
    private string ConvertGitignoreToGlob(string gitignorePattern)
    {
        var pattern = gitignorePattern;

        // Handle Negation (Not Fully Supported, Just Strip)
        if (pattern.StartsWith('!'))
        {
            pattern = pattern[1..];
        }

        // Handle Directory-Only Patterns
        if (pattern.EndsWith('/'))
        {
            pattern = pattern + "**";
        }

        // Add Leading Wildcard If Pattern Doesn't Start With /
        if (!pattern.StartsWith('/') && !pattern.StartsWith("**/"))
        {
            pattern = "**/" + pattern;
        }

        // Remove Leading Slash
        if (pattern.StartsWith('/'))
        {
            pattern = pattern[1..];
        }

        return pattern;
    }

    /// <summary>
    /// Simple Glob Pattern Matching.
    /// </summary>
    private bool MatchesPattern(string path, string pattern)
    {
        // Convert Glob Pattern To Simple Matching
        var normalizedPattern = pattern.Replace('\\', '/');

        // Handle ** (Match Any Directories)
        if (normalizedPattern.Contains("**"))
        {
            var parts = normalizedPattern.Split(["**"], StringSplitOptions.None);

            if (parts.Length == 2)
            {
                var prefix = parts[0].TrimEnd('/');
                var suffix = parts[1].TrimStart('/');

                // Check If Path Contains The Pattern Parts
                if (String.IsNullOrEmpty(prefix) && String.IsNullOrEmpty(suffix))
                {
                    return true;
                }

                if (String.IsNullOrEmpty(prefix))
                {
                    return path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                           path.Contains("/" + suffix, StringComparison.OrdinalIgnoreCase);
                }

                if (String.IsNullOrEmpty(suffix))
                {
                    return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                           path.Contains(prefix + "/", StringComparison.OrdinalIgnoreCase);
                }

                return path.Contains(prefix, StringComparison.OrdinalIgnoreCase) &&
                       path.Contains(suffix, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Handle * (Match Any File Name)
        if (normalizedPattern.Contains('*'))
        {
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(normalizedPattern)
                .Replace("\\*", ".*") + "$";

            return System.Text.RegularExpressions.Regex.IsMatch
            (
                path,
                regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }

        // Exact Match Or Contains
        return path.Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/" + normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
               path.Contains(normalizedPattern + "/", StringComparison.OrdinalIgnoreCase);
    }
}
