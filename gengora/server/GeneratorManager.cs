using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace BITS.Gengora.Server;

public class GeneratorManager(string workspaceRoot)
{
    private readonly string _WorkspaceRoot = workspaceRoot;
    private string? _GeneratorProjectPath;
    private string? _BuiltAssemblyPath;

    public Task EnsureMSBuildAsync()
    {
        // We keep the method for compatibility but we no longer require MSBuildWorkspace in this prototype.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks if a .csproj file contains the generator marker.
    /// </summary>
    private async Task<bool> IsGeneratorProjectAsync(string csprojPath, CancellationToken ct)
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

    /// <summary>
    /// Finds generator project using marker-based discovery.
    /// Priority: 1) User-specified path, 2) Auto-discovery via marker scan
    /// </summary>
    public async Task<bool> FindAndOpenGeneratorProjectAsync(CancellationToken ct)
    {
        await this.EnsureMSBuildAsync();

        // Priority 1: User-specified path (GENERATOR_PROJECT_PATH)
        var envProjectPath = System.Environment.GetEnvironmentVariable(Constants.Environment.GENERATOR_PROJECT_PATH);

        // Try specific .csproj file first
        if (!string.IsNullOrEmpty(envProjectPath))
        {
            var projectPath = Path.IsPathRooted(envProjectPath) 
                ? envProjectPath 
                : Path.Combine(this._WorkspaceRoot, envProjectPath);

            // Direct .csproj file
            if (File.Exists(projectPath) && projectPath.EndsWith(Constants.Patterns.CSPROJ_EXTENSION, StringComparison.OrdinalIgnoreCase))
            {
                if (await this.IsGeneratorProjectAsync(projectPath, ct))
                {
                    this._GeneratorProjectPath = projectPath;
                    return true;
                }
            }
            
            // Folder containing .csproj
            if (Directory.Exists(projectPath))
            {
                var csprojs = Directory.GetFiles(projectPath, "*" + Constants.Patterns.CSPROJ_EXTENSION, SearchOption.TopDirectoryOnly);
                foreach (var csproj in csprojs)
                {
                    if (await this.IsGeneratorProjectAsync(csproj, ct))
                    {
                        this._GeneratorProjectPath = csproj;
                        return true;
                    }
                }
            }
        }

        // Priority 2: Auto-discovery - scan workspace for marker
        
        var allCsprojs = Directory.GetFiles(this._WorkspaceRoot, "*" + Constants.Patterns.CSPROJ_EXTENSION, SearchOption.AllDirectories);
        
        foreach (var csproj in allCsprojs)
        {
            // Skip bin/obj folders
            if (csproj.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                csproj.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            if (await this.IsGeneratorProjectAsync(csproj, ct))
            {
                this._GeneratorProjectPath = csproj;
                return true;
            }
        }

        await Console.Error.WriteLineAsync("[Gengora] No generator project found with marker");
        return false;
    }

    /// <summary>
    /// Gets the currently loaded generator project path.
    /// </summary>
    public string? GetCurrentProjectPath()
    {
        return this._GeneratorProjectPath;
    }

    public class BuildResult
    {
        public bool Success { get; set; }
        public List<SimpleDiagnostic> Diagnostics { get; } = [];
        public string? BuiltAssemblyPath { get; set; }
    }

    public async Task<BuildResult> BuildGeneratorAsync(CancellationToken ct)
    {
        if (String.IsNullOrEmpty(this._GeneratorProjectPath))
        {
            throw new InvalidOperationException(Constants.ErrorMessages.GENERATOR_PROJECT_NOT_LOADED);
        }

        var projDir = Path.GetDirectoryName(this._GeneratorProjectPath) ?? this._WorkspaceRoot;

        var psi = new ProcessStartInfo
        {
            FileName = Constants.Build.DOTNET_COMMAND,
            Arguments = String.Format(Constants.Build.BUILD_ARGS_TEMPLATE, this._GeneratorProjectPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = projDir
        };

        var result = new BuildResult();

        var proc = Process.Start(psi)!;

        var output = new StringBuilder();
        proc.OutputDataReceived += (s, e) => 
        { 
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };
        proc.ErrorDataReceived += (s, e) => 
        { 
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(ct);

        var full = output.ToString();
        var exitCode = proc.ExitCode;

        // Only log on build failure
        if (exitCode != 0)
        {
            await Console.Error.WriteLineAsync($"[Gengora ERROR] Build failed with exit code {exitCode}");
            await Console.Error.WriteLineAsync($"[Gengora ERROR] Output:\n{full}");
        }

        // Parse MSBuild-style diagnostics like: /path/File.cs(12,34): error CS1002: ; expected
        var lines = full.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var rx = new Regex(@"^(.*\.[a-zA-Z0-9_]+)\((\d+),(\d+)\):\s*(error|warning)\s+([^:]+):\s*(.*)$");

        foreach (var line in lines)
        {
            var m = rx.Match(line);

            if (m.Success)
            {
                var file = m.Groups[1].Value;
                var sl = Int32.Parse(m.Groups[2].Value) - 1;
                var sc = Int32.Parse(m.Groups[3].Value) - 1;
                var severity = m.Groups[4].Value;
                var code = m.Groups[5].Value;
                var msg = m.Groups[6].Value;

                result.Diagnostics.Add(new SimpleDiagnostic
                {
                    FilePath = file,
                    StartLine = sl,
                    StartChar = sc,
                    EndLine = sl,
                    EndChar = sc + 1,
                    Message = msg,
                    Severity = severity,
                    Code = code
                });
            }
        }

        result.Success = exitCode == 0;

        if (result.Success)
        {
            // locate the produced dll under bin/Debug/net8.0
            var projName = Path.GetFileNameWithoutExtension(this._GeneratorProjectPath);
            var candidate = Path.Combine(projDir, Constants.Build.BIN_FOLDER, Constants.Build.DEBUG_CONFIG, Constants.Build.TARGET_FRAMEWORK, projName + Constants.Build.DLL_EXTENSION);

            if (File.Exists(candidate))
            {
                this._BuiltAssemblyPath = candidate;
                result.BuiltAssemblyPath = candidate;
            }
        }

        return result;
    }

    public async Task<string?> EmitGeneratorAssemblyAsync(string? builtAssemblyPath, string outputDir, CancellationToken ct)
    {
        if (String.IsNullOrEmpty(builtAssemblyPath) || !File.Exists(builtAssemblyPath))
        {
            return null;
        }

        Directory.CreateDirectory(outputDir);
        var dest = Path.Combine(outputDir, Path.GetFileName(builtAssemblyPath));
        File.Copy(builtAssemblyPath, dest, overwrite: true);

        // Also copy auxiliary files (runtimeconfig, deps) so `dotnet <assembly>` can run from the output dir.
        var baseName = Path.GetFileNameWithoutExtension(builtAssemblyPath);
        var dir = Path.GetDirectoryName(builtAssemblyPath) ?? String.Empty;
        var runtimeConfig = Path.Combine(dir, baseName + Constants.Build.RUNTIME_CONFIG_EXTENSION);
        var deps = Path.Combine(dir, baseName + Constants.Build.DEPS_EXTENSION);
        var pdb = Path.Combine(dir, baseName + Constants.Build.PDB_EXTENSION);

        try
        {
            if (File.Exists(runtimeConfig))
            {
                File.Copy(runtimeConfig, Path.Combine(outputDir, Path.GetFileName(runtimeConfig)), overwrite: true);
            }
        }
        catch
        {
            // Ignore copy failures
        }

        try
        {
            if (File.Exists(deps))
            {
                File.Copy(deps, Path.Combine(outputDir, Path.GetFileName(deps)), overwrite: true);
            }
        }
        catch { }

        try
        {
            if (File.Exists(pdb))
            {
                File.Copy(pdb, Path.Combine(outputDir, Path.GetFileName(pdb)), overwrite: true);
            }
        }
        catch { }

        return dest;
    }
}
