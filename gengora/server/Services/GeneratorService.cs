using System.Text.Json;
using System.Diagnostics;
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
    private readonly List<System.IO.FileSystemWatcher> _OutputWatchers = new();
    private readonly Dictionary<string, DateTime> _recentlyReported = new(StringComparer.OrdinalIgnoreCase);


    
    // Tracks the folder this server instance currently owns (if any).
    private string? _ownedProjectFolder = null;
    private bool _ownsGenerator = false;
    // Session identifier for the running generator process (if started by this server)
    private string? _ownedGeneratorSessionId = null;

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

        // Initialize manual stop state from environment so server startup can respect user choice
        var manualFlag = Environment.GetEnvironmentVariable("GENGORA_MANUALLY_STOPPED");
        this._IsManuallyStopped = !string.IsNullOrEmpty(manualFlag) && (manualFlag.Equals("true", StringComparison.OrdinalIgnoreCase) || manualFlag == "1");

        // Wire up process output handlers
        this._ProcessManager.OnStdout += this.HandleGeneratorStdoutLine;
        this._ProcessManager.OnStderr += this.HandleGeneratorStderrLine;
        
        // Wire up observation mode changes
        this._ObservationManager.OnModeChanged += (oldMode, newMode) =>
        {
            _ = this.HandleObservationModeChangedAsync(oldMode, newMode);
        };
    }

    private void StartOutputWatchers(string projectFolder)
    {
        if (string.IsNullOrEmpty(projectFolder)) return;

        StopOutputWatchers();

        // DEBUG: Log the project folder being used
        _ = Console.Error.WriteLineAsync($"[Gengora] StartOutputWatchers called with projectFolder: {projectFolder}");

        // Candidate directories to watch for generated files
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Project folder itself - many generators may emit into output folders under the project
        candidates.Add(projectFolder);

        // Common output folder within project
        candidates.Add(Path.Combine(projectFolder, Constants.Directories.VSCODE_FOLDER, Constants.Directories.GENERATOR_FOLDER));

        // Search upward for repo-level gengora-output within a few parent levels, but only within
        // the server's current workspace root so other VS Code instances (different roots)
        // don't pick up generated files for this instance.
        var serverWorkspaceRoot = Directory.GetCurrentDirectory() ?? string.Empty;
        var parent = Directory.GetParent(projectFolder);
        var levels = 0;
        while (parent != null && levels < 6)
        {
            // Only add parents that are still under this server's workspace root; this avoids
            // cross-instance detection when multiple VS Code windows open different roots.
            try
            {
                if (!string.IsNullOrEmpty(serverWorkspaceRoot) &&
                    !parent.FullName.StartsWith(serverWorkspaceRoot, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                // Add the parent folder itself so we catch newly-created gengora-output directories
                candidates.Add(parent.FullName);

                var candidate = Path.Combine(parent.FullName, "gengora-output");
                if (Directory.Exists(candidate))
                {
                    candidates.Add(candidate);
                }
            }
            catch
            {
                // Defensive - if path comparisons fail for any reason, skip this parent level
                break;
            }

            parent = parent.Parent;
            levels++;
        }

        foreach (var dir in candidates)
        {
            if (string.IsNullOrEmpty(dir)) continue;
            // Prefer explicit out subfolder if it exists
            var pathToWatch = dir;
            if (Directory.Exists(Path.Combine(dir, Constants.Directories.OUT_FOLDER)))
            {
                pathToWatch = Path.Combine(dir, Constants.Directories.OUT_FOLDER);
            }

            try
            {
                if (!Directory.Exists(pathToWatch))
                {
                    _ = Console.Error.WriteLineAsync($"[Gengora] Skipping watcher for non-existent path: {pathToWatch}");
                    continue;
                }

                _ = Console.Error.WriteLineAsync($"[Gengora] Setting up file watcher for: {pathToWatch}");

                var watcher = new System.IO.FileSystemWatcher(pathToWatch)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.CreationTime | System.IO.NotifyFilters.LastWrite
                };

                watcher.Created += (s, e) => _ = Task.Run(() => OnGeneratedFileDetected(e.FullPath, projectFolder));
                watcher.Changed += (s, e) => _ = Task.Run(() => OnGeneratedFileDetected(e.FullPath, projectFolder));

                watcher.EnableRaisingEvents = true;
                this._OutputWatchers.Add(watcher);
            }
            catch (Exception ex)
            {
                // ignore watcher failures
                _ = Console.Error.WriteLineAsync($"[Gengora] Failed to set up watcher for {pathToWatch}: {ex.Message}");
            }
        }
    }

    private void StopOutputWatchers()
    {
        foreach (var w in this._OutputWatchers)
        {
            try
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            catch { }
        }

        this._OutputWatchers.Clear();
        this._recentlyReported.Clear();
    }

    private async Task OnGeneratedFileDetected(string fullPath, string projectPath)
    {
        try
        {
            if (string.IsNullOrEmpty(fullPath)) return;

            // DEBUG: Log every file detection attempt
            _ = Console.Error.WriteLineAsync($"[Gengora] File detected by watcher: {fullPath}");

            var fileName = Path.GetFileName(fullPath) ?? string.Empty;
            if (!fileName.StartsWith("generated-", StringComparison.OrdinalIgnoreCase))
            {
                _ = Console.Error.WriteLineAsync($"[Gengora] File does not match 'generated-' pattern: {fileName}");
                return;
            }

            // Deduplicate within a short window
            lock (_recentlyReported)
            {
                if (_recentlyReported.TryGetValue(fullPath, out var ts))
                {
                    if ((DateTime.UtcNow - ts).TotalSeconds < 2)
                    {
                        _ = Console.Error.WriteLineAsync($"[Gengora] File already reported recently: {fullPath}");
                        return;
                    }
                }

                _recentlyReported[fullPath] = DateTime.UtcNow;
            }

            // Determine whether this server instance is allowed to report generated-file events
            // for the project folder. If the project is currently owned by another VS Code window's
            // server instance, do not forward the event from here to avoid mirrored notifications.
            try
            {
                if (!this.CanReportGeneratedFilesForProject(projectPath))
                {
                    // another server owns this project — ignore this event
                    _ = Console.Error.WriteLineAsync($"[Gengora] Another server owns project {projectPath}, skipping notification");
                    return;
                }
            }
            catch
            {
                // If there's any error determining ownership, fall back to conservative behavior and send the notification.
            }

            // Notify client about generated files
            this._LanguageServer.SendNotification(Constants.Notifications.GENERATOR_GENERATED, new
            {
                project = projectPath,
                created = new[] { fullPath }
            });

            _ = Console.Error.WriteLineAsync($"[Gengora] Sent GENERATOR_GENERATED notification for: {fullPath}");

            // Log to stderr to help debugging when running without a client
            await Console.Error.WriteLineAsync($"[Gengora] Generated file detected: {fullPath}");
        }
        catch (Exception ex)
        {
            // Swallow any watcher errors
            _ = Console.Error.WriteLineAsync($"[Gengora] Exception in OnGeneratedFileDetected: {ex}");
        }
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
            
            if (!this._IsPaused && !this._IsManuallyStopped)
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

        // Prefer an already loaded project (e.g. TryOpenProjectAtPathAsync may have set this earlier)
        var projectAlready = this._GeneratorManager.GetCurrentProjectPath();
        var found = false;

        if (!string.IsNullOrEmpty(projectAlready))
        {
            found = true;
            await Console.Error.WriteLineAsync($"[Gengora] StartGeneratorAsync: using pre-loaded project '{projectAlready}'");
        }
        else
        {
            await this.SendStatusAsync(Constants.States.COMPILING, null, null, cancellationToken);
            found = await this._GeneratorManager.FindAndOpenGeneratorProjectAsync(cancellationToken);
        }

        if (!found)
        {
            await this.SendStatusAsync(Constants.States.ERROR, Constants.ErrorMessages.PROJECT_NOT_FOUND, null, cancellationToken);
            await Console.Error.WriteLineAsync($"[Gengora] StartGeneratorAsync: No generator project found in workspace and none pre-loaded.");

            return;
        }

        // Update observation manager with discovered project (or ensure manager's project path is used)
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

        // Determine the project folder where the generator lives
        var projDir = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory();

        // Be conservative: if the discovered project folder is not contained within this
        // server instance's workspace root (the process CWD), do not attempt to manage
        // it here. This prevents one server instance from attempting to own/manage
        // generators that are outside its configured workspace.
        try
        {
            var serverRoot = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullProjDir = Path.GetFullPath(projDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            // On macOS '/var' is a symlink to '/private/var' — normalize checks to accept
            // either representation as 'inside' the workspace root.
            var serverRootCandidates = new List<string> { serverRoot };
            if (serverRoot.StartsWith("/private/", StringComparison.OrdinalIgnoreCase))
            {
                serverRootCandidates.Add(serverRoot.Substring("/private".Length));
            }
            else
            {
                // If not already prefixed, check the variant with /private to be safe
                serverRootCandidates.Add(Path.Combine("/private", serverRoot.TrimStart(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar));
            }

            // (path normalization checks done above)

            if (!serverRootCandidates.Any(r => fullProjDir.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
            {
                var msg = $"Generator project '{projDir}' is outside this server's workspace root '{serverRoot}'; skipping management here.";
                await this.SendStatusAsync(Constants.States.OBSERVING_FULL, msg, null, cancellationToken);
                await Console.Error.WriteLineAsync($"[Gengora] Skipping start - project '{projDir}' is outside server workspace root '{serverRoot}'");
                return;
            }
        }
        catch
        {
            // If anything goes wrong, proceed conservatively.
        }




        // Attempt to build and start the generator with retries on failures (use conservative retry policy)
        var maxAttempts = Constants.Timeouts.START_RETRY_COUNT;
        var retryMs = Constants.Timeouts.START_RETRY_INTERVAL_MS;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await this.SendStatusAsync(Constants.States.COMPILING, attempt > 1 ? $"retry {attempt}/{maxAttempts}" : null, null, cancellationToken);

                var build = await this._GeneratorManager.BuildGeneratorAsync(cancellationToken);

                if (!build.Success)
                {
                    await this.PublishBuildDiagnosticsAsync(build, cancellationToken);
                    var msg = $"Compilation failed (attempt {attempt}/{maxAttempts})";
                    await this.SendStatusAsync(Constants.States.ERROR, Constants.ErrorMessages.COMPILATION_FAILED, null, cancellationToken);
                    await Console.Error.WriteLineAsync($"[Gengora] {msg}");
                    this._LanguageServer.SendNotification(Constants.Notifications.GENERATOR_ERROR, new { message = msg, attempt });

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(retryMs, cancellationToken);
                        continue;
                    }

                    return;
                }

                // Publish any warnings
                await this.PublishBuildDiagnosticsAsync(build, cancellationToken);

                // Place emitted generator assembly under the generator project's folder so multi-root
                // workspaces place artifacts next to the generator project instead of the server CWD.
                // projDir already computed above
                var outDir = Path.Combine(projDir, Constants.Directories.VSCODE_FOLDER, Constants.Directories.GENERATOR_FOLDER, Constants.Directories.OUT_FOLDER);
                var assemblyPath = await this._GeneratorManager.EmitGeneratorAssemblyAsync(build.BuiltAssemblyPath!, outDir, cancellationToken);

                if (assemblyPath == null)
                {
                    var msg = $"Emit failed (attempt {attempt}/{maxAttempts})";
                    await this.SendStatusAsync(Constants.States.ERROR, Constants.ErrorMessages.EMIT_FAILED, null, cancellationToken);
                    await Console.Error.WriteLineAsync($"[Gengora] {msg}");
                    this._LanguageServer.SendNotification(Constants.Notifications.GENERATOR_ERROR, new { message = msg, attempt });

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(retryMs, cancellationToken);
                        continue;
                    }

                    return;
                }

                await this.SendStatusAsync(Constants.States.COMPILED, null, assemblyPath, cancellationToken);

                // Ensure we start the generator with the generator project's directory as the working directory
                var workspaceRoot = Path.GetDirectoryName(this._GeneratorManager.GetCurrentProjectPath()) ?? projDir;

                await this._ProcessManager.StopProcessAsync(TimeSpan.FromSeconds(Constants.Timeouts.GRACEFUL_SHUTDOWN_SECONDS));

                // Claim ownership before launching the process so other servers won't prematurely forward events
                this._ownedProjectFolder = projDir;
                this._ownsGenerator = true;

                // Generate a session token for this generator run and record it so the generator
                // can include it in messages. This provides a runtime handshake so different server
                // instances can be reliably associated with their generator process.
                this._ownedGeneratorSessionId = Guid.NewGuid().ToString("N");

                try
                {
                    var procEnv = new Dictionary<string, string>()
                    {
                        ["GENGORA_SESSION_ID"] = this._ownedGeneratorSessionId ?? string.Empty
                    };

                    await this._ProcessManager.StartProcessAsync(assemblyPath, null, workspaceRoot, cancellationToken, procEnv);
                    await this.SendStatusAsync(Constants.States.RUNNING, null, assemblyPath, cancellationToken);

                    // Start file system watchers to detect any 'generated-*' output produced by the generator
                    try
                    {
                        StartOutputWatchers(workspaceRoot);
                    }
                    catch
                    {
                        // best effort - do not fail startup
                    }

                    // successful start - done
                    return;
                }
                catch (Exception ex)
                {
                    // Failed to start process - log and retry
                    var msg = $"Failed to start generator (attempt {attempt}/{maxAttempts}): {ex.Message}";
                    await Console.Error.WriteLineAsync($"[Gengora] {msg}\n{ex}");
                    this._LanguageServer.SendNotification(Constants.Notifications.GENERATOR_ERROR, new { message = msg, stack = ex.ToString(), attempt });

                    // Clean up ownership on failure
                    this._ownsGenerator = false;
                    this._ownedGeneratorSessionId = null;

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(retryMs, cancellationToken);
                        continue;
                    }

                    await this.SendStatusAsync(Constants.States.ERROR, msg, null, cancellationToken);
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Abort early if cancellation requested
                await Console.Error.WriteLineAsync($"[Gengora] StartGeneratorAsync aborted due to cancellation");
                throw;
            }
            catch (Exception ex)
            {
                // Unexpected exception while compiling/handling build - surface info and retry
                var msg = $"Unexpected error during start (attempt {attempt}/{maxAttempts}): {ex.Message}";
                await Console.Error.WriteLineAsync($"[Gengora] {msg}\n{ex}");
                this._LanguageServer.SendNotification(Constants.Notifications.GENERATOR_ERROR, new { message = msg, stack = ex.ToString(), attempt });

                if (attempt < maxAttempts)
                {
                    await Task.Delay(retryMs, cancellationToken);
                    continue;
                }

                await this.SendStatusAsync(Constants.States.ERROR, msg, null, cancellationToken);
                return;
            }
        }

        // All build/start attempts either succeeded and returned above or failed and were handled.
    }

    public async Task StopGeneratorAsync(CancellationToken cancellationToken)
    {
        this._IsPaused = false;
        this._IsManuallyStopped = true; // Mark as manually stopped - prevents auto-restart
        await this.SendStatusAsync(Constants.States.STOPPING, null, null, cancellationToken);
        await this._ProcessManager.StopProcessAsync(TimeSpan.FromSeconds(Constants.Timeouts.GRACEFUL_SHUTDOWN_SECONDS));
        await this.SendStatusAsync(Constants.States.STOPPED, null, null, cancellationToken);

        // Stop any file watchers attached to the generator output
        try { StopOutputWatchers(); } catch { }

        this._ownedProjectFolder = null;
        this._ownsGenerator = false;
        this._ownedGeneratorSessionId = null;
    }

    public async Task PauseGeneratorAsync(CancellationToken cancellationToken)
    {
        this._IsPaused = true;
        await this.SendStatusAsync(Constants.States.PAUSED, "Generator paused", null, cancellationToken);
        await this._ProcessManager.StopProcessAsync(TimeSpan.FromSeconds(Constants.Timeouts.GRACEFUL_SHUTDOWN_SECONDS));

        try { StopOutputWatchers(); } catch { }

        this._ownedProjectFolder = null;
        this._ownsGenerator = false;
        this._ownedGeneratorSessionId = null;
    }

    public async Task RestartGeneratorAsync(CancellationToken cancellationToken)
    {
        // Don't restart if paused
        if (this._IsPaused)
        {
            return;
        }

        // Don't restart if user manually stopped
        if (this._IsManuallyStopped)
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
            // Always start fresh (don't check if already running) - this enables hot-reload
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
            // If we don't currently have a project loaded, a new .csproj appearing
            // might be the generator project - attempt to open it directly
            if (string.IsNullOrEmpty(this._ObservationManager.CurrentProjectPath))
            {
                var opened = await this._GeneratorManager.TryOpenProjectAtPathAsync(filePath, cancellationToken);
                if (opened)
                {
                    // Notify observation manager and start generator if appropriate
                    await this._ObservationManager.SetGeneratorProjectAsync(filePath, cancellationToken);
                    if (!this._IsPaused && !this._IsManuallyStopped)
                    {
                        await this.StartGeneratorAsync(cancellationToken);
                    }
                    return;
                }
            }

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
        // DEBUG: Log every line from generator
        _ = Console.Error.WriteLineAsync($"[Gengora] HandleGeneratorStdoutLine received: {line}");

        // Try to parse single-line JSON messages emitted by the generator
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("method", out var methodElem) && methodElem.ValueKind == JsonValueKind.String)
            {
                var method = methodElem.GetString() ?? string.Empty;
                // If the generator provided a sessionId in params, attempt to validate against our
                // owned generator session. This helps ensure we only honor messages from the
                // generator instance that was started by this server. Maintain backward
                // compatibility: if no sessionId is provided, accept the message.
                string? incomingSessionId = null;
                if (root.TryGetProperty("params", out var paramsElem) && paramsElem.ValueKind == JsonValueKind.Object)
                {
                    if (paramsElem.TryGetProperty("sessionId", out var sessionElem) && sessionElem.ValueKind == JsonValueKind.String)
                    {
                        incomingSessionId = sessionElem.GetString();
                    }
                }

                if (this._ownsGenerator && !string.IsNullOrEmpty(this._ownedGeneratorSessionId))
                {
                    _ = Console.Error.WriteLineAsync($"[Gengora] DEBUG: Checking session ID - owned: {this._ownedGeneratorSessionId}, incoming: {incomingSessionId}");
                    
                    if (incomingSessionId != null)
                    {
                        if (!string.Equals(incomingSessionId, this._ownedGeneratorSessionId, StringComparison.OrdinalIgnoreCase))
                        {
                            // Mismatched session - ignore to avoid cross-instance noise
                            Console.Error.WriteLine($"[Gengora] Ignoring generator message due to session mismatch (expected {this._ownedGeneratorSessionId}, got {incomingSessionId})");
                            return;
                        }
                    }
                    else
                    {
                        // No sessionId present; accept for backward compatibility but log a note
                        Console.Error.WriteLine("[Gengora] Warning: generator message received without sessionId while server owns generator - accepting for compatibility.");
                    }
                }
                
                // Handle handshake/capabilities
                if (method == Constants.Notifications.GENERATOR_HELLO)
                {
                    this.ParseGeneratorCapabilities(root);
                }

                // Forward to client - the language server will handle notification routing
                // For now, send as custom notification
                _ = Console.Error.WriteLineAsync($"[Gengora] Forwarding generator notification: {method}");
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

    // Check if this server can report generated files for the project
    // (removed lock-based ownership checks - now always allows reporting)
    public bool CanReportGeneratedFilesForProject(string? projectFolder)
    {
        // Always allow reporting - no file locks
        return true;
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
