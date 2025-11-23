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

    public async Task<bool> FindAndOpenGeneratorProjectAsync(CancellationToken ct)
    {
        await this.EnsureMSBuildAsync();

        // First, if a specific project path is provided via environment, prefer that.
        var envSpecified = System.Environment.GetEnvironmentVariable(Constants.Environment.GENERATOR_PROJECT_PATH);

        if (!string.IsNullOrEmpty(envSpecified) && File.Exists(envSpecified))
        {
            this._GeneratorProjectPath = envSpecified;

            return true;
        }

        // Search strategy:
        // 1. Look for "Gengora" or "Generator" folder at top level
        // 2. Find Generator.csproj within that folder
        // 3. Exclude server, extension, .vscode folders
        
        string? csproj = null;
        
        // Try Gengora folder first (current naming)
        var gengoraFolder = Path.Combine(this._WorkspaceRoot, Constants.Patterns.GENGORA_FOLDER_NAME);
        if (Directory.Exists(gengoraFolder))
        {
            var csprojs = Directory.GetFiles(gengoraFolder, Constants.Patterns.CSPROJ_PATTERN, SearchOption.TopDirectoryOnly);
            csproj = csprojs.FirstOrDefault();
        }
        
        // Try Generator folder (fallback)
        if (csproj is null)
        {
            var generatorFolder = Path.Combine(this._WorkspaceRoot, Constants.Patterns.GENERATOR_FOLDER_NAME);
            if (Directory.Exists(generatorFolder))
            {
                var csprojs = Directory.GetFiles(generatorFolder, Constants.Patterns.CSPROJ_PATTERN, SearchOption.TopDirectoryOnly);
                csproj = csprojs.FirstOrDefault();
            }
        }
        
        // Fallback: search all directories but exclude server/extension/.vscode
        if (csproj is null)
        {
            var allCsprojs = Directory.GetFiles(this._WorkspaceRoot, Constants.Patterns.GENERATOR_PROJECT_NAME, SearchOption.AllDirectories)
                .Where(p => !Constants.Patterns.EXCLUDED_FOLDERS.Any(excluded => p.Contains(Path.DirectorySeparatorChar + excluded + Path.DirectorySeparatorChar)))
                .ToArray();
            
            csproj = allCsprojs.FirstOrDefault();
        }

        if (csproj is null)
        {
            return false;
        }

        this._GeneratorProjectPath = csproj;

        return true;
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

        // debug: log raw build output so it's visible in server stderr
        await Console.Error.WriteLineAsync("[GeneratorManager] build output:\n" + full);
        await Console.Error.WriteLineAsync("[GeneratorManager] exit code: " + proc.ExitCode);

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

        result.Success = proc.ExitCode == 0;

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
