using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace Gengora.Tools.E2eTest;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // try to locate the repo root and the built server DLL; avoid relying solely on AppContext.BaseDirectory
        var initialGuess = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var serverDll = Path.Combine(initialGuess, "server", "bin", "Release", "net8.0", "BITS.Gengora.Server.dll");

        // If not found where we expect, walk up from the current working dir to locate the server DLL
        if (!File.Exists(serverDll))
        {
            var fallback = FindAncestorContaining(Directory.GetCurrentDirectory(), p => File.Exists(Path.Combine(p, "server", "bin", "Release", "net8.0", "BITS.Gengora.Server.dll")), 8);
            if (fallback != null)
            {
                serverDll = Path.Combine(fallback, "server", "bin", "Release", "net8.0", "BITS.Gengora.Server.dll");
            }
        }

        if (!File.Exists(serverDll))
        {
            Console.Error.WriteLine($"[e2e_test] Server DLL not found at expected locations (tried {initialGuess} and working directory ancestors)");
            return 2;
        }

        var repoRoot = Path.GetFullPath(Path.Combine(serverDll, "..", "..", "..", "..", ".."));
        var sample = Path.Combine(repoRoot, "test-workspace");

        if (!Directory.Exists(sample))
        {
            Console.Error.WriteLine($"[e2e_test] Sample test-workspace not found at: {sample}");
            return 2;
        }

        var tmp = Path.Combine(Path.GetTempPath(), "gengora-e2e-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        Directory.CreateDirectory(tmp);
        Console.WriteLine($"[e2e_test] Created workspace: {tmp}");

        var psi = new ProcessStartInfo("dotnet")
        {
            Arguments = $"\"{serverDll}\" --workspace-root \"{tmp}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null)
        {
            Console.Error.WriteLine("[e2e_test] failed to start server process");
            return 2;
        }

        try
        {
            // Initialize LSP
            var init = new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { processId = (object?)null, rootUri = (object?)null, capabilities = new { } } };
            Console.WriteLine("[e2e_test] Sending initialize");
            await SendLspAsync(proc.StandardInput.BaseStream, init);

            var initialized = new { jsonrpc = "2.0", method = "initialized", @params = new { } };
            await SendLspAsync(proc.StandardInput.BaseStream, initialized);

            Console.WriteLine("[e2e_test] Reading server init messages (expecting no generator)");
            var msgs = await ReadLspMessagesAsync(proc.StandardOutput.BaseStream, TimeSpan.FromSeconds(4));
            foreach (var m in msgs)
            {
                Console.WriteLine("INIT >> " + m);
            }

            // Copy sample project into workspace
            var genDir = Path.Combine(tmp, "gen1");
            Console.WriteLine("[e2e_test] Creating generator project at " + genDir);
            CopyDirectory(sample, genDir);

            var csproj = Path.Combine(genDir, "Gengora.csproj");
            var uri = new Uri(csproj).AbsoluteUri;

            // Notify server of created csproj
            var notify = new
            {
                jsonrpc = "2.0",
                method = "workspace/didChangeWatchedFiles",
                @params = new
                {
                    changes = new[] { new { uri = uri, type = 1 } }
                }
            };

            Console.WriteLine("[e2e_test] Notifying server of created csproj " + uri);
            await SendLspAsync(proc.StandardInput.BaseStream, notify);

            Console.WriteLine("[e2e_test] Waiting for compile/run...");
            var msgs2 = await ReadLspMessagesAsync(proc.StandardOutput.BaseStream, TimeSpan.FromSeconds(30));
            foreach (var m in msgs2)
            {
                Console.WriteLine("MSG << " + m);
            }

            var s = string.Join(" ", msgs2);
            if (s.Contains("compiled") && s.Contains("running"))
            {
                Console.WriteLine("[e2e_test] Server compiled and started generator — looking for generator messages...");
            }
            else
            {
                Console.WriteLine("[e2e_test] Compilation or run not observed — server may have failed or timed out");
            }

            // Wait a few seconds for generator to produce files
            Console.WriteLine("[e2e_test] Giving generator a few seconds to produce files...");
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Search for generated files in locations scoped to the generator project
            var foundAny = false;
            var candidates = new List<string>();

            // repo-level gengora-output (if present in the repository)
            var repoGengoraOutput = Path.Combine(repoRoot, "gengora-output");
            if (Directory.Exists(repoGengoraOutput)) candidates.Add(repoGengoraOutput);

            // prefer the generator project itself and any known generator output folder under it
            candidates.Add(genDir);
            candidates.Add(Path.Combine(genDir, ".vscode", ".generator", "out"));

            foreach (var baseRoot in candidates)
            {
                try
                {
                    if (!Directory.Exists(baseRoot)) continue;
                    foreach (var f in SafeEnumerateFiles(baseRoot, "generated-*"))
                    {
                        Console.WriteLine($"[e2e_test] Found generated file at {f}");
                        foundAny = true;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // skip unreadable folders
                }
                catch (IOException)
                {
                    // skip IO errors
                }
            }

            if (!foundAny)
            {
                Console.WriteLine("[e2e_test] No generated files discovered (generator may place output elsewhere).");
            }

            // Remove project and notify
            Console.WriteLine("[e2e_test] Removing project folder to simulate deletion...");
            try { Directory.Delete(genDir, true); } catch { }

            var notifyDel = new
            {
                jsonrpc = "2.0",
                method = "workspace/didChangeWatchedFiles",
                @params = new
                {
                    changes = new[] { new { uri = uri, type = 3 } }
                }
            };

            await SendLspAsync(proc.StandardInput.BaseStream, notifyDel);

            var msgs3 = await ReadLspMessagesAsync(proc.StandardOutput.BaseStream, TimeSpan.FromSeconds(4));
            foreach (var m in msgs3)
            {
                Console.WriteLine("DEL> " + m);
            }

            Console.WriteLine("[e2e_test] Requesting shutdown → exit");
            await SendLspAsync(proc.StandardInput.BaseStream, new { jsonrpc = "2.0", id = 99, method = "shutdown", @params = (object?)null });
            await Task.Delay(300);
            await SendLspAsync(proc.StandardInput.BaseStream, new { jsonrpc = "2.0", method = "exit", @params = new { } });

            try { proc.WaitForExit(5000); } catch { }

            Console.WriteLine("[e2e_test] Test finished, server exited");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[e2e_test] Error during test: " + ex);
            try { proc.Kill(); } catch { }
            return 2;
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    private static string? FindAncestorContaining(string start, Func<string, bool> predicate, int maxDepth)
    {
        var cur = new DirectoryInfo(start);
        for (var i = 0; i < maxDepth && cur != null; i++)
        {
            if (predicate(cur.FullName)) return cur.FullName;
            cur = cur.Parent;
        }

        return null;
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        var src = new DirectoryInfo(sourceDir);
        var dst = new DirectoryInfo(targetDir);
        dst.Create();

        foreach (var file in src.GetFiles())
        {
            var destFile = Path.Combine(dst.FullName, file.Name);
            file.CopyTo(destFile, true);
        }

        foreach (var dir in src.GetDirectories())
        {
            CopyDirectory(dir.FullName, Path.Combine(dst.FullName, dir.Name));
        }
    }

    private static async Task SendLspAsync(Stream stdin, object payload)
    {
        var body = JsonSerializer.Serialize(payload);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bodyBytes.Length}\r\n\r\n");
        await stdin.WriteAsync(header, 0, header.Length);
        await stdin.WriteAsync(bodyBytes, 0, bodyBytes.Length);
        await stdin.FlushAsync();
    }

    private static async Task<List<string>> ReadLspMessagesAsync(Stream stdout, TimeSpan timeout)
    {
        var end = DateTime.UtcNow.Add(timeout);
        var mem = new MemoryStream();
        var results = new List<string>();
        var buffer = new byte[4096];

        while (DateTime.UtcNow < end)
        {
            var readTask = stdout.ReadAsync(buffer, 0, buffer.Length);
            var completed = await Task.WhenAny(readTask, Task.Delay(200));
            if (completed == readTask)
            {
                var n = readTask.Result;
                if (n <= 0)
                {
                    await Task.Delay(50);
                    continue;
                }

                mem.Write(buffer, 0, n);
            }
            else
            {
                await Task.Delay(10);
            }

            bool processed;
            do
            {
                processed = false;
                var bytes = mem.ToArray();
                var s = Encoding.UTF8.GetString(bytes);
                var idx = s.IndexOf("Content-Length:", StringComparison.OrdinalIgnoreCase);
                if (idx < 0) break;
                var sepIndex = s.IndexOf("\r\n\r\n", idx);
                if (sepIndex < 0) break;
                var header = s.Substring(idx, sepIndex - idx);
                var parts = header.Split(':', 2);
                if (parts.Length < 2)
                {
                    mem = new MemoryStream(bytes, sepIndex + 4, Math.Max(0, bytes.Length - (sepIndex + 4)));
                    break;
                }

                if (!int.TryParse(parts[1].Trim(), out var length))
                {
                    mem = new MemoryStream(bytes, sepIndex + 4, Math.Max(0, bytes.Length - (sepIndex + 4)));
                    break;
                }

                var totalNeeded = sepIndex + 4 + length;
                if (bytes.Length < totalNeeded) break;

                var body = Encoding.UTF8.GetString(bytes, sepIndex + 4, length);
                results.Add(body);

                var remainder = bytes.Length - totalNeeded;
                var newMem = new MemoryStream();
                if (remainder > 0)
                {
                    newMem.Write(bytes, totalNeeded, remainder);
                }

                mem = newMem;
                processed = true;
            } while (processed);
        }

        return results;
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root, string pattern)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            string[] files;
            try
            {
                files = Directory.GetFiles(dir, pattern);
            }
            catch (UnauthorizedAccessException)
            {
                // skip directories we cannot access
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var f in files)
                yield return f;

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(dir);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var d in subdirs)
                stack.Push(d);
        }
    }
}
