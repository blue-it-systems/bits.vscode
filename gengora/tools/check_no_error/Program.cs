using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Gengora.Tools.CheckNoError;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var serverDll = Path.Combine(repoRoot, "server", "bin", "Release", "net8.0", "BITS.Gengora.Server.dll");

        if (!File.Exists(serverDll))
        {
            Console.Error.WriteLine($"[check_no_error] Server DLL not found at: {serverDll}");
            return 2;
        }

        var tmp = Path.Combine(Path.GetTempPath(), "gengora-check-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        Directory.CreateDirectory(tmp);
        Console.WriteLine($"[check_no_error] workspace: {tmp}");

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
            Console.Error.WriteLine("[check_no_error] failed to start server process");
            return 2;
        }

        try
        {
            var init = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new { processId = (object?)null, rootUri = (object?)null, capabilities = new { } }
            };

            await SendLspAsync(proc.StandardInput.BaseStream, init);

            var initialized = new { jsonrpc = "2.0", method = "initialized", @params = new { } };
            await SendLspAsync(proc.StandardInput.BaseStream, initialized);

            var msgs = await ReadLspMessagesAsync(proc.StandardOutput.BaseStream, TimeSpan.FromSeconds(4));
            foreach (var m in msgs)
            {
                Console.WriteLine(">> " + m);
            }

            var combined = string.Join(" ", msgs);
            if (combined.Contains("\"state\":\"error\"") || combined.Contains("Generator project not found"))
            {
                Console.WriteLine("[check_no_error] server emitted error state during init");
                await SendLspAsync(proc.StandardInput.BaseStream, new { jsonrpc = "2.0", id = 99, method = "shutdown", @params = (object?)null });
                await Task.Delay(300);
                await SendLspAsync(proc.StandardInput.BaseStream, new { jsonrpc = "2.0", method = "exit", @params = new { } });
                try { proc.WaitForExit(3000); } catch { }
                return 1;
            }

            Console.WriteLine("[check_no_error] Server did not emit an error during init — OK");

            // graceful shutdown
            await SendLspAsync(proc.StandardInput.BaseStream, new { jsonrpc = "2.0", id = 99, method = "shutdown", @params = (object?)null });
            await Task.Delay(300);
            await SendLspAsync(proc.StandardInput.BaseStream, new { jsonrpc = "2.0", method = "exit", @params = new { } });

            try { proc.WaitForExit(3000); } catch { }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[check_no_error] Error: " + ex);
            try { proc.Kill(); } catch { }
            return 2;
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
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
                // no data in this iteration
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
}
