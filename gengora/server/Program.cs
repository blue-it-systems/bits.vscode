namespace BITS.Gengora.Server;

internal static class LspMessage
{
    public static async Task<string?> ReadOneAsync(Stream input, CancellationToken ct)
    {
        // Read headers
        var reader = new StreamReader(input, Encoding.ASCII, false, 2048, leaveOpen: true);
        string? line;
        int contentLength = 0;

        // Read header lines until empty line
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                break;

            var parts = line.Split(':', 2);

            if (parts.Length == 2 && parts[0].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(parts[1].Trim(), out contentLength);
            }
        }

        if (contentLength <= 0) return null;

        var buffer = new char[contentLength];
        int read = 0;

        while (read < contentLength)
        {
            int n = await reader.ReadAsync(buffer, read, contentLength - read);

            if (n == 0)
                break;

            read += n;
        }

        return new string(buffer, 0, read);
    }

    public static void Send(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        var stdout = Console.OpenStandardOutput();
        stdout.Write(header, 0, header.Length);
        stdout.Write(bytes, 0, bytes.Length);
        stdout.Flush();
    }

    public static void SendNotification(string method, object? parameters)
    {
        var obj = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = parameters
        };
        Send(obj);
    }

    public static void SendResponse(object id, object? result)
    {
        var obj = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result
        };
        Send(obj);
    }

    public static void SendError(object id, int code, string message)
    {
        var obj = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new Dictionary<string, object?> { ["code"] = code, ["message"] = message }
        };
        Send(obj);
    }
}

internal class Program
{
    private class GeneratorCapabilities
    {
        public bool PublishDiagnostics { get; set; } = false;
        public bool WatchMode { get; set; } = false;
        public string[] WatchGlobs { get; set; } = Array.Empty<string>();
        public int WatchDebounceMs { get; set; } = 400;
    }
    static GeneratorManager _generatorManager = null!; // will be initialized in Main
    static ProcessManager _processManager = new ProcessManager();
    // capabilities announced by the running generator (via JSON handshake)
    static GeneratorCapabilities _generatorCapabilities = new GeneratorCapabilities();
    static CancellationTokenSource _stopCts = new CancellationTokenSource();

    public static async Task Main(string[] args)
    {
        // Determine workspace root from args (cli: --workspace-root <path>) or default to CWD
        string workspaceRoot = Directory.GetCurrentDirectory();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--workspace-root" && (i + 1) < args.Length)
            {
                workspaceRoot = args[i + 1];
                break;
            }
        }

        _generatorManager = new GeneratorManager(workspaceRoot);
        // Pipe generator stdout/stderr back to the client via custom notifications
        // But prefer parsing & forwarding structured single-line JSON messages the generator emits.
        _processManager.OnStdout += (line) => HandleGeneratorStdoutLine(line);
        _processManager.OnStderr += (line) => LspMessage.SendNotification("$/generator.stderr", new { text = line });

        var input = Console.OpenStandardInput();
        var ct = _stopCts.Token;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var body = await LspMessage.ReadOneAsync(input, ct);

                if (body == null)
                {
                    await Task.Delay(10, ct);
                    
                    continue;
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("method", out var methodProp))
                {
                    var method = methodProp.GetString();

                    // Request handling (has id) or notification
                    bool isRequest = root.TryGetProperty("id", out var idProp);

                    if (method == "initialize")
                    {
                        if (isRequest)
                        {
                            // reply with minimal capabilities
                            object id = idProp.ValueKind == JsonValueKind.Number ? (object)idProp.GetInt32() : idProp.GetString() ?? string.Empty;
                            var result = new
                            {
                                capabilities = new
                                {
                                    // advertise both the old 'generator.*' commands and the new 'gengora.*' commands for compatibility
                                    executeCommandProvider = new { commands = new[] { "generator.start", "generator.stop", "gengora.start", "gengora.stop" } },
                                    textDocumentSync = 1
                                }
                            };
                            LspMessage.SendResponse(id!, result);
                        }
                    }
                    else if (method == "initialized")
                    {
                        // no response
                    }
                    else if (method == "shutdown")
                    {
                        if (isRequest)
                        {
                            object id = idProp.ValueKind == JsonValueKind.Number ? (object)idProp.GetInt32() : idProp.GetString() ?? string.Empty;
                            LspMessage.SendResponse(id, null);
                            _stopCts.Cancel();
                        }
                    }
                    else if (method == "exit")
                    {
                        Environment.Exit(0);
                    }
                    else if (method == "workspace/executeCommand")
                    {
                        // executeCommand is a request with id
                        object id = idProp.ValueKind == JsonValueKind.Number ? (object)idProp.GetInt32() : idProp.GetString() ?? "0";
                        // reply immediately to avoid blocking client
                        LspMessage.SendResponse(id, null);

                        // Handle in background
                        _ = HandleExecuteCommandAsync(root, ct);
                    }
                    else if (method == "workspace/didChangeWatchedFiles")
                    {
                        // Notification. Trigger a compile/restart in background.
                        _ = HandleWatchedFilesChangedAsync(ct);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Main loop error: " + ex.Message);
            }
        }

        // Clean shutdown
        try { await _processManager.StopProcessAsync(TimeSpan.FromSeconds(2)); } catch { }
    }

    private static void HandleGeneratorStdoutLine(string line)
    {
        // Try to parse single-line JSON messages emitted by the generator.
        // If message contains a 'method' string, forward it as a LSP notification and handle handshake events.
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("method", out var methodElem) && methodElem.ValueKind == JsonValueKind.String)
            {
                var method = methodElem.GetString() ?? string.Empty;
                object? @params = null;
                if (root.TryGetProperty("params", out var p))
                {
                    // convert params to a strongly-typed object for sending over LSP
                    @params = JsonSerializer.Deserialize<object>(p.GetRawText());
                }

                // Store handshake/capabilities if this is the initial hello message
                if (method == "generator/hello")
                {
                    try
                    {
                        if (root.TryGetProperty("params", out var p2) && p2.ValueKind == JsonValueKind.Object && p2.TryGetProperty("capabilities", out var caps))
                        {
                            _generatorCapabilities.PublishDiagnostics = caps.TryGetProperty("publishDiagnostics", out var pd) && pd.GetBoolean();
                            _generatorCapabilities.WatchMode = caps.TryGetProperty("watchMode", out var wm) && wm.GetBoolean();
                            if (caps.TryGetProperty("watchGlobs", out var wg) && wg.ValueKind == JsonValueKind.Array)
                            {
                                var list = new List<string>();
                                foreach (var item in wg.EnumerateArray()) if (item.ValueKind == JsonValueKind.String) list.Add(item.GetString()!);
                                _generatorCapabilities.WatchGlobs = list.ToArray();
                            }
                            if (caps.TryGetProperty("watchDebounceMs", out var db) && db.ValueKind == JsonValueKind.Number)
                            {
                                _generatorCapabilities.WatchDebounceMs = db.GetInt32();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Failed to parse generator hello: " + ex.Message);
                    }
                }

                // If generator is sending structured diagnostics, forward to the LSP diagnostics pipeline
                if (method == "generator/publishDiagnostics" || method == "generator.publishDiagnostics")
                {
                    // Expect params to be { uri: string, diagnostics: [...] }
                    LspMessage.SendNotification("textDocument/publishDiagnostics", @params);
                    return;
                }

                // Forward structured events to the client
                LspMessage.SendNotification(method, @params);
                return;
            }
        }
        catch (JsonException) { /* not a JSON message, fall through */ }
        catch (Exception ex)
        {
            Console.Error.WriteLine("HandleGeneratorStdoutLine parse error: " + ex.Message);
        }

        // fallback: send raw stdout line
        LspMessage.SendNotification("$/generator.stdout", new { text = line });
    }

    private static async Task HandleWatchedFilesChangedAsync(CancellationToken ct)
    {
        // Debounce simple
        await Task.Delay(500, ct).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnRanToCompletion);
        try
        {
            // If the running generator claims to manage its own watch-mode, don't trigger a coordinator-level recompile
            if (_generatorCapabilities != null && _generatorCapabilities.WatchMode)
            {
                LspMessage.SendNotification("$/generator.status", new { state = "watch-skipped", message = "Generator manages its own watch-mode; coordinator will not recompile automatically." });
                return;
            }
            await LspMessageTask(async () =>
            {
                LspMessage.SendNotification("$/generator.status", new { state = "compiling" });
                var found = await _generatorManager.FindAndOpenGeneratorProjectAsync(ct);
                if (!found)
                {
                    LspMessage.SendNotification("$/generator.status", new { state = "error", message = "Generator project not found" });
                    return;
                }

                var build = await _generatorManager.BuildGeneratorAsync(ct);
                if (!build.Success)
                {
                    // publish any diagnostics (errors/warnings)
                    foreach (var d in build.Diagnostics)
                    {
                        var uri = new Uri(d.Code.StartsWith("/") ? d.Code : d.Code).AbsoluteUri; // not ideal, diagnostics do not carry filename here
                    }
                    LspMessage.SendNotification("$/generator.status", new { state = "error", message = "Compilation failed" });
                    // continue to publish structured diagnostics per file
                    foreach (var kv in GroupDiagnosticsByFile(build))
                {
                    var uri = new Uri(kv.Key).AbsoluteUri;
                    var diagList = new List<object>();
                    foreach (var d in kv.Value)
                    {
                        int severity = d.Severity == "Error" ? 1 : d.Severity == "Warning" ? 2 : 3;
                        diagList.Add(new
                        {
                            range = new { start = new { line = d.StartLine, character = d.StartChar }, end = new { line = d.EndLine, character = d.EndChar } },
                            message = d.Message,
                            severity = severity,
                            source = "generator-compile",
                            code = d.Code
                        });
                    }

                        LspMessage.SendNotification("textDocument/publishDiagnostics", new { uri = uri, diagnostics = diagList });
                    }
                }
                else
                {
                    // publish diagnostics (warnings) if any
                    foreach (var kv in GroupDiagnosticsByFile(build))
                    {
                        var uri = new Uri(kv.Key).AbsoluteUri;
                        var diagList = new List<object>();
                        foreach (var d in kv.Value)
                        {
                            int severity = d.Severity == "Error" ? 1 : d.Severity == "Warning" ? 2 : 3;
                            diagList.Add(new
                            {
                                range = new { start = new { line = d.StartLine, character = d.StartChar }, end = new { line = d.EndLine, character = d.EndChar } },
                                message = d.Message,
                                severity = severity,
                                source = "generator-compile",
                                code = d.Code
                            });
                        }

                        LspMessage.SendNotification("textDocument/publishDiagnostics", new { uri = uri, diagnostics = diagList });
                    }
                }

                var outDir = Path.Combine(Directory.GetCurrentDirectory(), ".vscode", ".generator", "out");
                var assemblyPath = await _generatorManager.EmitGeneratorAssemblyAsync(build.BuiltAssemblyPath, outDir, ct);
                if (assemblyPath == null)
                {
                    LspMessage.SendNotification("$/generator.status", new { state = "error", message = "Emit failed" });
                    return;
                }

                LspMessage.SendNotification("$/generator.status", new { state = "compiled", path = assemblyPath });

                await _processManager.StopProcessAsync(TimeSpan.FromSeconds(2));
                await _processManager.StartProcessAsync(assemblyPath, null, ct);
                LspMessage.SendNotification("$/generator.status", new { state = "running", path = assemblyPath });
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("HandleWatchedFilesChangedAsync: " + ex.Message);
        }
    }

    private static async Task HandleExecuteCommandAsync(JsonElement requestRoot, CancellationToken ct)
    {
        try
        {
            var root = requestRoot;
            if (!root.TryGetProperty("params", out var p)) return;

            if (p.TryGetProperty("command", out var cmdElement))
            {
                var cmd = cmdElement.GetString() ?? string.Empty;
                if (cmd == "generator.start" || cmd == "gengora.start")
                {
                    LspMessage.SendNotification("$/generator.status", new { state = "compiling" });
                    var found = await _generatorManager.FindAndOpenGeneratorProjectAsync(ct);
                    if (!found)
                    {
                        LspMessage.SendNotification("$/generator.status", new { state = "error", message = "Generator project not found" });
                        return;
                    }

                    var build = await _generatorManager.BuildGeneratorAsync(ct);
                    if (!build.Success)
                    {
                        LspMessage.SendNotification("$/generator.status", new { state = "error", message = "Compilation failed" });
                        // publish diagnostics
                        foreach (var kv in GroupDiagnosticsByFile(build))
                        {
                            var uri = new Uri(kv.Key).AbsoluteUri;
                            var diagList = new List<object>();
                            foreach (var d in kv.Value)
                            {
                                int severity = d.Severity == "Error" ? 1 : d.Severity == "Warning" ? 2 : 3;
                                diagList.Add(new
                                {
                                    range = new { start = new { line = d.StartLine, character = d.StartChar }, end = new { line = d.EndLine, character = d.EndChar } },
                                    message = d.Message,
                                    severity = severity,
                                    source = "generator-compile",
                                    code = d.Code
                                });
                            }

                            LspMessage.SendNotification("textDocument/publishDiagnostics", new { uri = uri, diagnostics = diagList });
                        }
                        return;
                    }

                    // publish diagnostics (warnings) if any
                    foreach (var kv in GroupDiagnosticsByFile(build))
                    {
                        var uri = new Uri(kv.Key).AbsoluteUri;
                        var diagList = new List<object>();
                        foreach (var d in kv.Value)
                        {
                            int severity = d.Severity == "Error" ? 1 : d.Severity == "Warning" ? 2 : 3;
                            diagList.Add(new
                            {
                                range = new { start = new { line = d.StartLine, character = d.StartChar }, end = new { line = d.EndLine, character = d.EndChar } },
                                message = d.Message,
                                severity = severity,
                                source = "generator-compile",
                                code = d.Code
                            });
                        }
                        LspMessage.SendNotification("textDocument/publishDiagnostics", new { uri = uri, diagnostics = diagList });
                    }

                    var outDir = Path.Combine(Directory.GetCurrentDirectory(), ".vscode", ".generator", "out");
                    var assemblyPath = await _generatorManager.EmitGeneratorAssemblyAsync(build.BuiltAssemblyPath, outDir, ct);
                    if (assemblyPath == null)
                    {
                        LspMessage.SendNotification("$/generator.status", new { state = "error", message = "Emit failed" });
                        return;
                    }

                    LspMessage.SendNotification("$/generator.status", new { state = "compiled", path = assemblyPath });

                    await _processManager.StopProcessAsync(TimeSpan.FromSeconds(2));
                    await _processManager.StartProcessAsync(assemblyPath, null, ct);
                    LspMessage.SendNotification("$/generator.status", new { state = "running", path = assemblyPath });
                }
                else if (cmd == "generator.stop" || cmd == "gengora.stop")
                {
                    LspMessage.SendNotification("$/generator.status", new { state = "stopping" });
                    await _processManager.StopProcessAsync(TimeSpan.FromSeconds(2));
                    LspMessage.SendNotification("$/generator.status", new { state = "stopped" });
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("HandleExecuteCommandAsync: " + ex.Message);
        }
    }

    // Helper to run a background task and report exceptions to stderr
    private static async Task LspMessageTask(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("LspMessageTask error: " + ex.Message);
        }
    }

    private static IDictionary<string, List<SimpleDiagnostic>> GroupDiagnosticsByFile(GeneratorManager.BuildResult build)
    {
        var dict = new Dictionary<string, List<SimpleDiagnostic>>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in build.Diagnostics)
        {
            var file = string.IsNullOrEmpty(d.FilePath) ? Path.Combine(Directory.GetCurrentDirectory(), "") : d.FilePath;
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
