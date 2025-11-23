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

        // Priority 1: Specific .csproj file path from environment (GENERATOR_PROJECT_PATH)
        var envProjectPath = System.Environment.GetEnvironmentVariable(Constants.Environment.GENERATOR_PROJECT_PATH);
        if (!string.IsNullOrEmpty(envProjectPath))
        {
            // If it's a .csproj file, use it directly
            if (File.Exists(envProjectPath) && envProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                this._GeneratorProjectPath = envProjectPath;
                return true;
            }
            
            // If it's a folder, look for .csproj inside
            if (Directory.Exists(envProjectPath))
            {
                var csprojs = Directory.GetFiles(envProjectPath, Constants.Patterns.CSPROJ_PATTERN, SearchOption.TopDirectoryOnly);
                var csproj = csprojs.FirstOrDefault();
                if (csproj != null)
                {
                    this._GeneratorProjectPath = csproj;
                    return true;
                }
            }
        }

        // Priority 2: Custom folder path from environment (GENERATOR_FOLDER_PATH)
        var envFolderPath = System.Environment.GetEnvironmentVariable(Constants.Environment.GENERATOR_FOLDER_PATH);
        if (!string.IsNullOrEmpty(envFolderPath))
        {
            var folderPath = Path.IsPathRooted(envFolderPath) 
                ? envFolderPath 
                : Path.Combine(this._WorkspaceRoot, envFolderPath);
            
            if (Directory.Exists(folderPath))
            {
                var csprojs = Directory.GetFiles(folderPath, Constants.Patterns.CSPROJ_PATTERN, SearchOption.TopDirectoryOnly);
                var csproj = csprojs.FirstOrDefault();
                if (csproj != null)
                {
                    this._GeneratorProjectPath = csproj;
                    return true;
                }
            }
        }

        // Priority 3: Default - Look ONLY in the Gengora folder
        var gengoraFolder = Path.Combine(this._WorkspaceRoot, Constants.Patterns.GENGORA_FOLDER_NAME);
        
        if (!Directory.Exists(gengoraFolder))
        {
            return false;
        }
        
        var defaultCsprojs = Directory.GetFiles(gengoraFolder, Constants.Patterns.CSPROJ_PATTERN, SearchOption.TopDirectoryOnly);
        var defaultCsproj = defaultCsprojs.FirstOrDefault();

        if (defaultCsproj is null)
        {
            return false;
        }

        this._GeneratorProjectPath = defaultCsproj;

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
