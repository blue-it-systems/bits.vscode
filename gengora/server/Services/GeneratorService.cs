namespace BITS.Gengora.Server.Services;

/// <summary>
/// Core service for managing generator compilation, execution, and lifecycle.
/// </summary>
public class GeneratorService : IGeneratorService
{
    private readonly GeneratorManager _GeneratorManager;
    private readonly ProcessManager _ProcessManager;
    private readonly ILanguageServerFacade _LanguageServer;
    private readonly GeneratorCapabilities _GeneratorCapabilities;

    public GeneratorService
    (
        GeneratorManager generatorManager,
        ProcessManager processManager,
        ILanguageServerFacade languageServer
    )
    {
        this._GeneratorManager = generatorManager;
        this._ProcessManager = processManager;
        this._LanguageServer = languageServer;
        this._GeneratorCapabilities = new GeneratorCapabilities();

        // Wire up process output handlers
        this._ProcessManager.OnStdout += this.HandleGeneratorStdoutLine;
        this._ProcessManager.OnStderr += this.HandleGeneratorStderrLine;
    }

    public GeneratorCapabilities GetCapabilities()
    {
        return this._GeneratorCapabilities;
    }

    public async Task StartGeneratorAsync(CancellationToken cancellationToken)
    {
        await this.SendStatusAsync(Constants.States.COMPILING, null, null, cancellationToken);

        var found = await this._GeneratorManager.FindAndOpenGeneratorProjectAsync(cancellationToken);
        
        if (!found)
        {
            await this.SendStatusAsync(Constants.States.ERROR, Constants.ErrorMessages.PROJECT_NOT_FOUND, null, cancellationToken);

            return;
        }

        var build = await this._GeneratorManager.BuildGeneratorAsync(cancellationToken);
        
        if (!build.Success)
        {
            await this.SendStatusAsync(Constants.States.ERROR, Constants.ErrorMessages.COMPILATION_FAILED, null, cancellationToken);
            await this.PublishBuildDiagnosticsAsync(build, cancellationToken);

            return;
        }

        // Publish any warnings
        await this.PublishBuildDiagnosticsAsync(build, cancellationToken);

        var outDir = Path.Combine(Directory.GetCurrentDirectory(), Constants.Directories.VSCODE_FOLDER, Constants.Directories.GENERATOR_FOLDER, Constants.Directories.OUT_FOLDER);
        var assemblyPath = await this._GeneratorManager.EmitGeneratorAssemblyAsync(build.BuiltAssemblyPath!, outDir, cancellationToken);
        
        if (assemblyPath == null)
        {
            await this.SendStatusAsync(Constants.States.ERROR, Constants.ErrorMessages.EMIT_FAILED, null, cancellationToken);

            return;
        }

        await this.SendStatusAsync(Constants.States.COMPILED, null, assemblyPath, cancellationToken);

        await this._ProcessManager.StopProcessAsync(TimeSpan.FromSeconds(Constants.Timeouts.GRACEFUL_SHUTDOWN_SECONDS));
        await this._ProcessManager.StartProcessAsync(assemblyPath, null, cancellationToken);
        
        await this.SendStatusAsync(Constants.States.RUNNING, null, assemblyPath, cancellationToken);
    }

    public async Task StopGeneratorAsync(CancellationToken cancellationToken)
    {
        await this.SendStatusAsync(Constants.States.STOPPING, null, null, cancellationToken);
        await this._ProcessManager.StopProcessAsync(TimeSpan.FromSeconds(Constants.Timeouts.GRACEFUL_SHUTDOWN_SECONDS));
        await this.SendStatusAsync(Constants.States.STOPPED, null, null, cancellationToken);
    }

    public async Task RestartGeneratorAsync(CancellationToken cancellationToken)
    {
        // Check if generator manages its own watch mode
        if (this._GeneratorCapabilities.WatchMode)
        {
            await this.SendStatusAsync(Constants.States.WATCH_SKIPPED, Constants.ErrorMessages.WATCH_MODE_SKIPPED, null, cancellationToken);

            return;
        }

        await this.StartGeneratorAsync(cancellationToken);
    }

    private void HandleGeneratorStdoutLine(string line)
    {
        // Try to parse single-line JSON messages emitted by the generator
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("method", out var methodElem) && methodElem.ValueKind == JsonValueKind.String)
            {
                var method = methodElem.GetString() ?? string.Empty;
                
                // Handle handshake/capabilities
                if (method == Constants.Notifications.GENERATOR_HELLO)
                {
                    this.ParseGeneratorCapabilities(root);
                }

                // Forward to client - the language server will handle notification routing
                // For now, send as custom notification
                this._LanguageServer.SendNotification(method, root.TryGetProperty("params", out var p) ? p : (object?)null);

                return;
            }
        }
        catch (JsonException)
        {
            // Not JSON, fall through to raw output
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"HandleGeneratorStdoutLine error: {ex.Message}");
        }

        // Fallback: send raw stdout
        this._LanguageServer.SendNotification(Constants.Notifications.GENERATOR_STDOUT, new GeneratorOutputParams { Text = line });
    }

    private void HandleGeneratorStderrLine(string line)
    {
        this._LanguageServer.SendNotification(Constants.Notifications.GENERATOR_STDERR, new GeneratorOutputParams { Text = line });
    }

    private void ParseGeneratorCapabilities(JsonElement root)
    {
        try
        {
            if (root.TryGetProperty("params", out var p) && 
                p.ValueKind == JsonValueKind.Object && 
                p.TryGetProperty("capabilities", out var caps))
            {
                this._GeneratorCapabilities.PublishDiagnostics = 
                    caps.TryGetProperty("publishDiagnostics", out var pd) && pd.GetBoolean();
                    
                this._GeneratorCapabilities.WatchMode = 
                    caps.TryGetProperty("watchMode", out var wm) && wm.GetBoolean();
                    
                if (caps.TryGetProperty("watchGlobs", out var wg) && wg.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();

                    foreach (var item in wg.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            list.Add(item.GetString()!);
                        }
                    }

                    this._GeneratorCapabilities.WatchGlobs = [.. list];
                }
                
                if (caps.TryGetProperty("watchDebounceMs", out var db) && db.ValueKind == JsonValueKind.Number)
                {
                    this._GeneratorCapabilities.WatchDebounceMs = db.GetInt32();
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to parse generator capabilities: {ex.Message}");
        }
    }

    private async Task SendStatusAsync(string state, string? message, string? path, CancellationToken cancellationToken)
    {
        var statusParams = new GeneratorStatusParams
        {
            State = state,
            Message = message,
            Path = path
        };

        this._LanguageServer.SendNotification(Constants.Notifications.GENERATOR_STATUS, statusParams);
        await Task.CompletedTask;
    }

    private async Task PublishBuildDiagnosticsAsync(GeneratorManager.BuildResult build, CancellationToken cancellationToken)
    {
        var grouped = this.GroupDiagnosticsByFile(build);

        foreach (var kv in grouped)
        {
            var uri = new Uri(kv.Key).AbsoluteUri;
            var diagnosticList = new List<Diagnostic>();

            foreach (var d in kv.Value)
            {
                var diagnostic = new Diagnostic
                {
                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                    (
                        new Position(d.StartLine, d.StartChar),
                        new Position(d.EndLine, d.EndChar)
                    ),
                    Message = d.Message,
                    Severity = (DiagnosticSeverity)Constants.DiagnosticSeverity.ToLspSeverity(d.Severity),
                    Source = "generator-compile",
                    Code = d.Code
                };

                diagnosticList.Add(diagnostic);
            }

            this._LanguageServer.TextDocument.SendNotification(new PublishDiagnosticsParams
            {
                Uri = OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri.From(uri),
                Diagnostics = new Container<Diagnostic>(diagnosticList)
            });
        }

        await Task.CompletedTask;
    }

    private IDictionary<string, List<SimpleDiagnostic>> GroupDiagnosticsByFile(GeneratorManager.BuildResult build)
    {
        var dict = new Dictionary<string, List<SimpleDiagnostic>>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var d in build.Diagnostics)
        {
            var file = String.IsNullOrEmpty(d.FilePath) 
                ? Path.Combine(Directory.GetCurrentDirectory(), string.Empty) 
                : d.FilePath;
                
            if (!dict.TryGetValue(file, out var list))
            {
                list = new List<SimpleDiagnostic>();
                dict[file] = list;
            }
            
            list.Add(d);
        }
        
        return dict;
    }
}
