namespace BITS.Gengora.Server;

public class ProcessManager
{
    private Process? _Process;
    private CancellationTokenSource? _OutputCts;

    // Events to report stdout/stderr lines to a caller (e.g. the LSP server)
    public event Action<string>? OnStdout;
    public event Action<string>? OnStderr;

    public bool IsRunning => this._Process != null && !this._Process.HasExited;

    public async Task StartProcessAsync(string assemblyPath, string? args = null, CancellationToken ct = default)
    {
        if (this.IsRunning)
        {
            throw new InvalidOperationException(Constants.ErrorMessages.PROCESS_ALREADY_RUNNING);
        }

        var psi = new ProcessStartInfo
        {
            FileName = Constants.Build.DOTNET_COMMAND,
            Arguments = $"\"{assemblyPath}\" {args}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        this._Process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        this._Process.Start();

        this._OutputCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => this.PumpOutputAsync(this._Process, this._OutputCts.Token));
    }

    public async Task StopProcessAsync(TimeSpan gracefulTimeout)
    {
        if (this._Process == null)
            return;
        
        try
        {
            if (!this._Process.HasExited)
            {
                // Try polite close
                try
                {
                    this._Process.CloseMainWindow();
                }
                catch
                {
                    // Ignore close window failures
                }

                var sw = Stopwatch.StartNew();
                
                while (!this._Process.HasExited && sw.Elapsed < gracefulTimeout)
                {
                    await Task.Delay(Constants.Timeouts.PROCESS_CHECK_DELAY_MS);
                }

                if (!this._Process.HasExited)
                {
                    this._Process.Kill(entireProcessTree: true);
                }
            }
        }
        finally
        {
            try
            {
                this._OutputCts?.Cancel();
            }
            catch
            {
                // Ignore cancellation failures
            }
            
            this._Process?.Dispose();
            this._Process = null;
        }
    }

    private async Task PumpOutputAsync(Process proc, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && !proc.HasExited)
            {
                var line = await proc.StandardOutput.ReadLineAsync();
                
                if (line == null)
                    break;
                
                try
                {
                    this.OnStdout?.Invoke(line);
                }
                catch
                {
                    // Ignore event handler failures
                }
                
                Console.Error.WriteLine("[generator stdout] " + line);
            }

            // drain error stream
            while (!ct.IsCancellationRequested && !proc.HasExited)
            {
                var line = await proc.StandardError.ReadLineAsync();
                
                if (line == null)
                    break;
                
                try
                {
                    this.OnStderr?.Invoke(line);
                }
                catch
                {
                    // Ignore event handler failures
                }
                
                Console.Error.WriteLine("[generator stderr] " + line);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Console.Error.WriteLine("PumpOutputAsync error: " + ex.Message);
        }
    }
}
