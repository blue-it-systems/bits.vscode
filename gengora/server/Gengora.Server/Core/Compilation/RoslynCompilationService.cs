namespace Gengora.Server.Core.Compilation;

using System.Diagnostics;
using System.Text.RegularExpressions;
using Gengora.Server.Core.Discovery;
using Microsoft.Extensions.Logging;

/// <summary>
/// Compiles Generator Projects Using dotnet CLI.
/// Implements Specification R-ARCH-3, R-ARCH-4, R3.*, R4.*.
/// </summary>
public sealed partial class DotnetCompilationService : IDisposable
{
    private readonly ILogger<DotnetCompilationService> _Logger;
    private bool _IsDisposed;

    public DotnetCompilationService(ILogger<DotnetCompilationService> logger)
    {
        this._Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Compiles A Generator Project And Returns The Result.
    /// </summary>
    public async Task<CompilationResult> CompileAsync
    (
        GeneratorProjectInfo project,
        CancellationToken cancellationToken = default
    )
    {
        this.ThrowIfDisposed();

        if (project == null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        var stopwatch = Stopwatch.StartNew();

        this._Logger.LogInformation("Compiling Project: {ProjectPath}", project.ProjectPath);

        try
        {
            // Use dotnet build command
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{project.ProjectPath}\" --configuration Debug --no-restore",
                WorkingDirectory = project.ProjectDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            var outputLines = new List<string>();
            var errorLines = new List<string>();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    outputLines.Add(e.Data);

                    this._Logger.LogDebug("Build Output: {Line}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    errorLines.Add(e.Data);

                    this._Logger.LogDebug("Build Error: {Line}", e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            stopwatch.Stop();

            var diagnostics = this.ParseDiagnostics(outputLines.Concat(errorLines));

            if (process.ExitCode == 0)
            {
                // Find The Output Assembly
                var assemblyPath = this.FindOutputAssembly(project);

                this._Logger.LogInformation
                (
                    "Compilation Succeeded: {OutputPath} ({Duration}ms)",
                    assemblyPath,
                    stopwatch.ElapsedMilliseconds
                );

                return new CompilationResult
                {
                    Success = true,
                    AssemblyPath = assemblyPath,
                    Diagnostics = diagnostics,
                    Duration = stopwatch.Elapsed
                };
            }
            else
            {
                this._Logger.LogWarning
                (
                    "Compilation Failed: Exit Code {ExitCode} ({Duration}ms)",
                    process.ExitCode,
                    stopwatch.ElapsedMilliseconds
                );

                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    ErrorMessage = $"Build Failed With Exit Code {process.ExitCode}",
                    Duration = stopwatch.Elapsed
                };
            }
        }
        catch (OperationCanceledException)
        {
            this._Logger.LogInformation("Compilation Cancelled");

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            this._Logger.LogError(ex, "Compilation Failed Unexpectedly");

            return new CompilationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    private string FindOutputAssembly(GeneratorProjectInfo project)
    {
        // Default Output Path
        var assemblyName = Path.GetFileNameWithoutExtension(project.ProjectPath);
        var outputDir = Path.Combine(project.ProjectDirectory, "bin", "Debug", "net10.0");
        var assemblyPath = Path.Combine(outputDir, assemblyName + ".dll");

        if (File.Exists(assemblyPath))
        {
            return assemblyPath;
        }

        // Try net9.0 as fallback
        outputDir = Path.Combine(project.ProjectDirectory, "bin", "Debug", "net9.0");
        assemblyPath = Path.Combine(outputDir, assemblyName + ".dll");

        if (File.Exists(assemblyPath))
        {
            return assemblyPath;
        }

        // Try net8.0 as fallback
        outputDir = Path.Combine(project.ProjectDirectory, "bin", "Debug", "net8.0");
        assemblyPath = Path.Combine(outputDir, assemblyName + ".dll");

        return assemblyPath;
    }

    private IReadOnlyList<CompilationDiagnostic> ParseDiagnostics(IEnumerable<string> lines)
    {
        var diagnostics = new List<CompilationDiagnostic>();

        // Pattern: file(line,col): severity code: message
        var regex = DiagnosticPattern();

        foreach (var line in lines)
        {
            var match = regex.Match(line);

            if (match.Success)
            {
                var severity = match.Groups["severity"].Value.ToLowerInvariant() switch
                {
                    "error" => DiagnosticSeverity.Error,
                    "warning" => DiagnosticSeverity.Warning,
                    _ => DiagnosticSeverity.Info
                };

                diagnostics.Add(new CompilationDiagnostic
                {
                    Id = match.Groups["code"].Value,
                    Message = match.Groups["message"].Value,
                    Severity = severity,
                    FilePath = match.Groups["file"].Value,
                    Line = Int32.TryParse(match.Groups["line"].Value, out var l) ? l : null,
                    Column = Int32.TryParse(match.Groups["col"].Value, out var c) ? c : null
                });
            }
        }

        return diagnostics;
    }

    [GeneratedRegex(@"(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s*(?<severity>error|warning)\s+(?<code>\w+):\s*(?<message>.+)")]
    private static partial Regex DiagnosticPattern();

    private void ThrowIfDisposed()
    {
        if (this._IsDisposed)
        {
            throw new ObjectDisposedException(nameof(DotnetCompilationService));
        }
    }

    public void Dispose()
    {
        if (!this._IsDisposed)
        {
            this._IsDisposed = true;
        }
    }
}
