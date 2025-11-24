using System.Text.Json;
using BITS.Gengora.Server.Models;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace BITS.Gengora.Server.Services;

/// <summary>
/// Core service for managing generator compilation, execution, and lifecycle with dynamic observation.
/// </summary>
public class GeneratorService : IGeneratorService
{
    private readonly GeneratorManager _GeneratorManager;
    private readonly ProcessManager _ProcessManager;
    private readonly ObservationManager _ObservationManager;
    private readonly ILanguageServerFacade _LanguageServer;
    private readonly GeneratorCapabilities _GeneratorCapabilities;
    private bool _IsPaused;
    private bool _IsManuallyStopped; // Prevents auto-restart when user manually stops
    
    // Standard ignore patterns for file watching
    private static readonly string[] DefaultIgnorePatterns = new[]
    {
        "/bin/",
        "/obj/",
        "/node_modules/",
        "/.git/",
        "/.vs/",
        "/.vscode/.generator/",
        "/packages/",
        "/.idea/",
        "/gengora-output/"
    };

    public GeneratorService
    (
        GeneratorManager generatorManager,
        ProcessManager processManager,
        ObservationManager observationManager,
        ILanguageServerFacade languageServer
    )
    {
        this._GeneratorManager = generatorManager;
        this._ProcessManager = processManager;
        this._ObservationManager = observationManager;
        this._LanguageServer = languageServer;
        this._GeneratorCapabilities = new GeneratorCapabilities();
        this._IsPaused = false;
        this._IsManuallyStopped = false;

        // Wire up process output handlers
        this._ProcessManager.OnStdout += this.HandleGeneratorStdoutLine;
        this._ProcessManager.OnStderr += this.HandleGeneratorStderrLine;
        
        // Wire up observation mode changes
        this._ObservationManager.OnModeChanged += (oldMode, newMode) =>
        {
            _ = this.HandleObservationModeChangedAsync(oldMode, newMode);
        };
    }

    private async Task HandleObservationModeChangedAsync(ObservationMode oldMode, ObservationMode newMode)
    {
        // Upgrade: minimal → full (marker became true)
        if (oldMode == ObservationMode.MinimalObservation && newMode == ObservationMode.FullObservation)
        {
            await this.SendStatusAsync(Constants.States.OBSERVING_FULL, "Generator marker detected, enabling full observation", null, CancellationToken.None);
            
            // Send observation mode changed notification to client
            var projectFolder = this._ObservationManager.CurrentProjectFolder;
            this._LanguageServer.SendNotification(Constants.Notifications.OBSERVATION_MODE_CHANGED, new
            {
                mode = "FullObservation",
                projectFolder = projectFolder
            });
            
            // Clean build artifacts before starting to avoid corrupted state
            await this._GeneratorManager.CleanGeneratorAsync(CancellationToken.None);
            
            if (!this._IsPaused)
            {
                await this.StartGeneratorAsync(CancellationToken.None);
            }
        }
        
        // Downgrade: full → minimal (marker removed)
        else if (oldMode == ObservationMode.FullObservation && newMode == ObservationMode.MinimalObservation)
        {
            await this.SendStatusAsync(Constants.States.OBSERVING_MINIMAL, "Generator marker removed, switching to minimal observation", null, CancellationToken.None);
            
            // Send observation mode changed notification to client
            this._LanguageServer.SendNotification(Constants.Notifications.OBSERVATION_MODE_CHANGED, new
            {
                mode = "MinimalObservation",
                projectFolder = (string?)null
            });
            
            await this.StopGeneratorAsync(CancellationToken.None);
            
            // Clean build artifacts after stopping
            await this._GeneratorManager.CleanGeneratorAsync(CancellationToken.None);
        }
    }

    public GeneratorCapabilities GetCapabilities()
    {
        return this._GeneratorCapabilities;
    }

    public async Task StartGeneratorAsync(CancellationToken cancellationToken)
    {
        this._IsManuallyStopped = false; // Clear flag - user explicitly started

        await this.SendStatusAsync(Constants.States.COMPILING, null, null, cancellationToken);

        var found = await this._GeneratorManager.FindAndOpenGeneratorProjectAsync(cancellationToken);
        
        if (!found)
        {
            await this.SendStatusAsync(Constants.States.ERROR, Constants.ErrorMessages.PROJECT_NOT_FOUND, null, cancellationToken);

            return;
        }

        // Update observation manager with discovered project
        var projectPath = this._GeneratorManager.GetCurrentProjectPath();
        
        if (!string.IsNullOrEmpty(projectPath))
        {
            await this._ObservationManager.SetGeneratorProjectAsync(projectPath, cancellationToken);
            
            // Send project discovered notification to client
            this._LanguageServer.SendNotification(Constants.Notifications.GENERATOR_PROJECT_DISCOVERED, new
            {
                projectPath = projectPath
            });
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

        var workspaceRoot = Path.GetDirectoryName(this._GeneratorManager.GetCurrentProjectPath()) ?? Directory.GetCurrentDirectory();
        
        await this._ProcessManager.StopProcessAsync(TimeSpan.FromSeconds(Constants.Timeouts.GRACEFUL_SHUTDOWN_SECONDS));
        await this._ProcessManager.StartProcessAsync(assemblyPath, null, workspaceRoot, cancellationToken);
        
        await this.SendStatusAsync(Constants.States.RUNNING, null, assemblyPath, cancellationToken);
    }

    public async Task StopGeneratorAsync(CancellationToken cancellationToken)
    {
        this._IsPaused = false;
        this._IsManuallyStopped = true; // Mark as manually stopped - prevents auto-restart
        await this.SendStatusAsync(Constants.States.STOPPING, null, null, cancellationToken);
        await this._ProcessManager.StopProcessAsync(TimeSpan.FromSeconds(Constants.Timeouts.GRACEFUL_SHUTDOWN_SECONDS));
        await this.SendStatusAsync(Constants.States.STOPPED, null, null, cancellationToken);
    }

    public async Task PauseGeneratorAsync(CancellationToken cancellationToken)
    {
        this._IsPaused = true;
        await this.SendStatusAsync(Constants.States.PAUSED, "Generator paused", null, cancellationToken);
        await this._ProcessManager.StopProcessAsync(TimeSpan.FromSeconds(Constants.Timeouts.GRACEFUL_SHUTDOWN_SECONDS));
    }

    public async Task RestartGeneratorAsync(CancellationToken cancellationToken)
    {
        // Don't restart if paused
        if (this._IsPaused)
        {
            return;
        }

        // Check if generator manages its own watch mode
        if (this._GeneratorCapabilities.WatchMode)
        {
            await this.SendStatusAsync(Constants.States.WATCH_SKIPPED, Constants.ErrorMessages.WATCH_MODE_SKIPPED, null, cancellationToken);

            return;
        }

        // Only restart if in full observation mode
        if (this._ObservationManager.CurrentMode == ObservationMode.FullObservation)
        {
            await this.StartGeneratorAsync(cancellationToken);
        }
    }

    public async Task SwitchProjectAsync(string projectPath, CancellationToken cancellationToken)
    {
        await Console.Error.WriteLineAsync($"[Gengora] Switching to project: {projectPath}");
        
        // Stop current generator
        await this.StopGeneratorAsync(cancellationToken);
        
        // Update observation manager with new project
        await this._ObservationManager.SetGeneratorProjectAsync(projectPath, cancellationToken);
        
        // Restart if in full observation mode and not paused/manually stopped
        if (this._ObservationManager.CurrentMode == ObservationMode.FullObservation && !this._IsPaused && !this._IsManuallyStopped)
        {
            await this.StartGeneratorAsync(cancellationToken);
        }
    }

    public async Task HandleFileChangeAsync(string filePath, CancellationToken cancellationToken)
    {
        // Check if file should be ignored
        if (this.ShouldIgnoreFile(filePath))
        {
            return;
        }
        
        // If it's a .csproj file, recheck marker
        if (filePath.EndsWith(Constants.Patterns.CSPROJ_EXTENSION, StringComparison.OrdinalIgnoreCase))
        {
            await this._ObservationManager.RecheckMarkerAsync(cancellationToken);
        }
        
        // If in full observation mode and not paused/manually stopped, trigger restart
        if (this._ObservationManager.CurrentMode == ObservationMode.FullObservation && !this._IsPaused && !this._IsManuallyStopped)
        {
            await this.RestartGeneratorAsync(cancellationToken);
        }
    }
    
    private bool ShouldIgnoreFile(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        
        // Check against default ignore patterns
        foreach (var pattern in DefaultIgnorePatterns)
        {
            if (normalizedPath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        // TODO: Add support for user-configured ignore patterns from settings
        // This could read from .gengoraignore or LSP configuration
        
        return false;
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
            Console.Error.WriteLine($"[Gengora] HandleGeneratorStdoutLine error: {ex.Message}");
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
            Console.Error.WriteLine($"[Gengora] Failed to parse generator capabilities: {ex.Message}");
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
