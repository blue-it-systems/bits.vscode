namespace Gengora.Server.Core.Execution;

using System.Diagnostics;
using Gengora.Server.Core.Discovery;
using Gengora.Server.Core.Messaging;
using Microsoft.Extensions.Logging;

/// <summary>
/// Executes Generator Assemblies And Captures Output.
/// Implements Specification R6.*, R7.*.
/// </summary>
public sealed class GeneratorExecutor : IDisposable
{
    private readonly ILogger<GeneratorExecutor> _Logger;
    private readonly MessageParser _MessageParser;
    private Process? _CurrentProcess;
    private bool _IsDisposed;

    /// <summary>
    /// Event Raised When A Message Is Received From The Generator.
    /// </summary>
    public event EventHandler<GeneratorMessage>? MessageReceived;

    /// <summary>
    /// Event Raised When A File Is Emitted By The Generator.
    /// </summary>
    public event EventHandler<FileEmittedEventArgs>? FileEmitted;

    public GeneratorExecutor
    (
        ILogger<GeneratorExecutor> logger,
        MessageParser messageParser
    )
    {
        this._Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this._MessageParser = messageParser ?? throw new ArgumentNullException(nameof(messageParser));
    }

    /// <summary>
    /// Gets Whether A Generator Is Currently Running.
    /// </summary>
    public bool IsRunning => this._CurrentProcess != null && !this._CurrentProcess.HasExited;

    /// <summary>
    /// Executes A Compiled Generator Assembly.
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync
    (
        string assemblyPath,
        GeneratorProjectInfo project,
        CancellationToken cancellationToken = default
    )
    {
        this.ThrowIfDisposed();

        if (String.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("Assembly Path Is Required", nameof(assemblyPath));
        }

        if (!File.Exists(assemblyPath))
        {
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = $"Assembly Not Found: {assemblyPath}",
                SessionId = String.Empty
            };
        }

        // R6.5: Generate Unique Session ID
        var sessionId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();
        var messages = new List<GeneratorMessage>();
        var emittedFiles = new List<string>();

        this._Logger.LogInformation
        (
            "Executing Generator: {AssemblyPath} (Session: {SessionId})",
            assemblyPath,
            sessionId
        );

        try
        {
            // Stop Any Previous Execution
            this.StopCurrentExecution();

            // R6.3: Execute With dotnet
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{assemblyPath}\"",
                WorkingDirectory = project.ProjectDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // R6.5: Pass Session ID Via Environment Variable
            startInfo.EnvironmentVariables["GENGORA_SESSION_ID"] = sessionId;

            this._CurrentProcess = new Process { StartInfo = startInfo };

            // R6.14: Parse JSON Lines From Stdout
            this._CurrentProcess.OutputDataReceived += (sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    this.ProcessOutputLine(e.Data, sessionId, project, messages, emittedFiles);
                }
            };

            // Capture Stderr For Diagnostics
            var stderrLines = new List<string>();

            this._CurrentProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    stderrLines.Add(e.Data);

                    this._Logger.LogDebug("Generator Stderr: {Line}", e.Data);
                }
            };

            this._CurrentProcess.Start();
            this._CurrentProcess.BeginOutputReadLine();
            this._CurrentProcess.BeginErrorReadLine();

            // Wait For Process To Exit
            await this._CurrentProcess.WaitForExitAsync(cancellationToken);

            stopwatch.Stop();

            var exitCode = this._CurrentProcess.ExitCode;
            var success = exitCode == 0;

            if (success)
            {
                this._Logger.LogInformation
                (
                    "Generator Completed: {EmittedCount} File(s) Emitted ({Duration}ms)",
                    emittedFiles.Count,
                    stopwatch.ElapsedMilliseconds
                );
            }
            else
            {
                this._Logger.LogWarning
                (
                    "Generator Failed With Exit Code: {ExitCode} ({Duration}ms)",
                    exitCode,
                    stopwatch.ElapsedMilliseconds
                );

                // Log stderr for debugging
                if (stderrLines.Count > 0)
                {
                    this._Logger.LogError("Generator Stderr:\n{Stderr}", String.Join(Environment.NewLine, stderrLines));
                }
            }

            var errorMessage = success ? null : (stderrLines.Count > 0
                ? $"Generator Failed (Exit Code {exitCode}): {String.Join(Environment.NewLine, stderrLines)}"
                : $"Generator Failed With Exit Code {exitCode}");

            return new ExecutionResult
            {
                Success = success,
                ExitCode = exitCode,
                Messages = messages,
                EmittedFiles = emittedFiles,
                Duration = stopwatch.Elapsed,
                SessionId = sessionId,
                ErrorMessage = errorMessage
            };
        }
        catch (OperationCanceledException)
        {
            this._Logger.LogInformation("Generator Execution Cancelled");

            this.StopCurrentExecution();

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            this._Logger.LogError(ex, "Generator Execution Failed");

            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed,
                SessionId = sessionId
            };
        }
        finally
        {
            this._CurrentProcess?.Dispose();
            this._CurrentProcess = null;
        }
    }

    private void ProcessOutputLine
    (
        string line,
        string expectedSessionId,
        GeneratorProjectInfo project,
        List<GeneratorMessage> messages,
        List<string> emittedFiles
    )
    {
        var message = this._MessageParser.Parse(line);

        if (message == null)
        {
            // R6.14: Non-JSON Lines Logged At Debug Level
            this._Logger.LogDebug("Generator Output (Non-JSON): {Line}", line);

            return;
        }

        // R6.7: Validate Session ID
        if (!this._MessageParser.ValidateSessionId(message, expectedSessionId))
        {
            // R8.5: Silent Discard On Session ID Mismatch
            return;
        }

        messages.Add(message);

        this.MessageReceived?.Invoke(this, message);

        // R6.10: Track Emitted Files
        if (message.IsFileEmitMessage && !String.IsNullOrWhiteSpace(message.Path))
        {
            // R6.16: Verify Path Is Not In Generator Source Tree
            if (!this._MessageParser.IsPathInProtectedDirectory(message.Path, project.ProjectDirectory))
            {
                emittedFiles.Add(message.Path);

                this.FileEmitted?.Invoke(this, new FileEmittedEventArgs(message.Path));

                this._Logger.LogDebug("Generator Emitted File: {Path}", message.Path);
            }
        }
    }

    /// <summary>
    /// Stops The Currently Running Generator Process.
    /// </summary>
    public void StopCurrentExecution()
    {
        if (this._CurrentProcess != null && !this._CurrentProcess.HasExited)
        {
            this._Logger.LogInformation("Stopping Generator Process");

            try
            {
                this._CurrentProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                this._Logger.LogWarning(ex, "Failed To Kill Generator Process");
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (this._IsDisposed)
        {
            throw new ObjectDisposedException(nameof(GeneratorExecutor));
        }
    }

    public void Dispose()
    {
        if (!this._IsDisposed)
        {
            this.StopCurrentExecution();
            this._IsDisposed = true;
        }
    }
}

/// <summary>
/// Event Arguments For File Emitted Events.
/// </summary>
public sealed class FileEmittedEventArgs : EventArgs
{
    public string FilePath { get; }

    public FileEmittedEventArgs(string filePath)
    {
        this.FilePath = filePath;
    }
}
