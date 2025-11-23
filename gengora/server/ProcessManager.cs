using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BITS.Gengora.Server
{
    public class ProcessManager
    {
        private Process? _process;
        private CancellationTokenSource? _outputCts;

        // Events to report stdout/stderr lines to a caller (e.g. the LSP server)
        public event Action<string>? OnStdout;
        public event Action<string>? OnStderr;

        public bool IsRunning => _process != null && !_process.HasExited;

        public async Task StartProcessAsync(string assemblyPath, string? args = null, CancellationToken ct = default)
        {
            if (IsRunning) throw new InvalidOperationException("Process already running");
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{assemblyPath}\" {args}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.Start();

            _outputCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ = Task.Run(() => PumpOutputAsync(_process, _outputCts.Token));
        }

        public async Task StopProcessAsync(TimeSpan gracefulTimeout)
        {
            if (_process == null) return;
            try
            {
                if (!_process.HasExited)
                {
                    // Try polite close
                    try { _process.CloseMainWindow(); } catch { }

                    var sw = Stopwatch.StartNew();
                    while (!_process.HasExited && sw.Elapsed < gracefulTimeout)
                    {
                        await Task.Delay(200);
                    }

                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
            }
            finally
            {
                try { _outputCts?.Cancel(); } catch { }
                _process?.Dispose();
                _process = null;
            }
        }

        private async Task PumpOutputAsync(Process proc, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && !proc.HasExited)
                {
                    var line = await proc.StandardOutput.ReadLineAsync();
                    if (line == null) break;
                    try { OnStdout?.Invoke(line); } catch { }
                    Console.Error.WriteLine("[generator stdout] " + line);
                }

                // drain error stream
                while (!ct.IsCancellationRequested && !proc.HasExited)
                {
                    var line = await proc.StandardError.ReadLineAsync();
                    if (line == null) break;
                    try { OnStderr?.Invoke(line); } catch { }
                    Console.Error.WriteLine("[generator stderr] " + line);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Console.Error.WriteLine("PumpOutputAsync error: " + ex.Message);
            }
        }
    }
}
