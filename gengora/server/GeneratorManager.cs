namespace BITS.Gengora.Server;

public class GeneratorManager(string workspaceRoot)
{
    private readonly string _WorkspaceRoot = workspaceRoot;

    private string? _GeneratorProjectPath;

    public Task EnsureMSBuildAsync()
    {
        // We keep the method for compatibility but we no longer require MSBuildWorkspace in this prototype.
        return Task.CompletedTask;
    }

    public async Task<bool> FindAndOpenGeneratorProjectAsync(CancellationToken ct)
    {
        await this.EnsureMSBuildAsync();

        // First, if a specific project path is provided via environment, prefer that.
        var envSpecified = Environment.GetEnvironmentVariable("GENERATOR_PROJECT_PATH");

        if (!string.IsNullOrEmpty(envSpecified) && File.Exists(envSpecified))
        {
            this._GeneratorProjectPath = envSpecified;

            return true;
        }

        // Heuristic: prefer an exact Generator.csproj in the workspace (or a folder named "Generator"),
        // otherwise fall back to the broader *Generator*.csproj pattern.
        var csproj = Directory.GetFiles(this._WorkspaceRoot, "Generator.csproj", SearchOption.AllDirectories).FirstOrDefault();

        if (csproj is null)
        {
            var genFolder = Directory.GetDirectories(this._WorkspaceRoot, "Generator", SearchOption.AllDirectories).FirstOrDefault();
            
            if (genFolder != null)
            {
                csproj = Directory.GetFiles(genFolder, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            }
        }

        csproj ??= Directory.GetFiles(this._WorkspaceRoot, "*Generator*.csproj", SearchOption.AllDirectories).FirstOrDefault();

        if (csproj is null)
        {
            var genFolder = Directory.GetDirectories(this._WorkspaceRoot, "Generator", SearchOption.AllDirectories).FirstOrDefault();
            if (genFolder != null)
            {
                csproj = Directory.GetFiles(genFolder, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            }
        }

        if (csproj is null)
            return false;

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
        if (string.IsNullOrEmpty(_GeneratorProjectPath)) throw new InvalidOperationException("Generator project not loaded");

        var projDir = Path.GetDirectoryName(this._GeneratorProjectPath) ?? this._WorkspaceRoot;

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{this._GeneratorProjectPath}\" --no-restore --nologo",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = projDir
        };

        var result = new BuildResult();

        var proc = System.Diagnostics.Process.Start(psi)!;

        var output = new StringBuilder();
        proc.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        proc.ErrorDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(ct);

        var full = output.ToString();

        // debug: log raw build output so it's visible in server stderr
        Console.Error.WriteLine("[GeneratorManager] build output:\n" + full);
        Console.Error.WriteLine("[GeneratorManager] exit code: " + proc.ExitCode);

        // Parse MSBuild-style diagnostics like: /path/File.cs(12,34): error CS1002: ; expected
        var lines = full.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var rx = new System.Text.RegularExpressions.Regex(@"^(.*\.[a-zA-Z0-9_]+)\((\d+),(\d+)\):\s*(error|warning)\s+([^:]+):\s*(.*)$");

        foreach (var line in lines)
        {
            var m = rx.Match(line);

            if (m.Success)
            {
                var file = m.Groups[1].Value;
                var sl = int.Parse(m.Groups[2].Value) - 1;
                var sc = int.Parse(m.Groups[3].Value) - 1;
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
            var projName = Path.GetFileNameWithoutExtension(_GeneratorProjectPath);
            var candidate = Path.Combine(projDir, "bin", "Debug", "net8.0", projName + ".dll");

            if (File.Exists(candidate))
                this._BuiltAssemblyPath = candidate;
        }

        return result;
    }

    public async Task<string?> EmitGeneratorAssemblyAsync(string builtAssemblyPath, string outputDir, CancellationToken ct)
    {
        if (String.IsNullOrEmpty(builtAssemblyPath) || !File.Exists(builtAssemblyPath))
            return null;

        Directory.CreateDirectory(outputDir);
        var dest = Path.Combine(outputDir, Path.GetFileName(builtAssemblyPath));
        File.Copy(builtAssemblyPath, dest, overwrite: true);

        // Also copy auxiliary files (runtimeconfig, deps) so `dotnet <assembly>` can run from the output dir.
        var baseName = Path.GetFileNameWithoutExtension(builtAssemblyPath);
        var dir = Path.GetDirectoryName(builtAssemblyPath) ?? String.Empty;
        var runtimeConfig = Path.Combine(dir, baseName + ".runtimeconfig.json");
        var deps = Path.Combine(dir, baseName + ".deps.json");
        var pdb = Path.Combine(dir, baseName + ".pdb");

        try
        {
             if (File.Exists(runtimeConfig))
             {
                 File.Copy(runtimeConfig, Path.Combine(outputDir, Path.GetFileName(runtimeConfig)), overwrite: true);
             }
        }
        catch { }

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
