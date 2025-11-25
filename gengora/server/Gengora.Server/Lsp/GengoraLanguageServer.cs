namespace Gengora.Server.Lsp;

using System.Reflection;
using Gengora.Server.Core;
using Gengora.Server.Core.Compilation;
using Gengora.Server.Core.Execution;
using Gengora.Server.Core.StateMachine;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;

/// <summary>
/// LSP Server Using StreamJsonRpc.
/// Implements Specification RPC Methods.
/// </summary>
public sealed class GengoraLanguageServer : IDisposable
{
    private readonly ILogger<GengoraLanguageServer> _Logger;
    private readonly GeneratorOrchestrator _Orchestrator;
    private JsonRpc? _Rpc;
    private bool _IsInitialized;
    private bool _IsDisposed;

    public GengoraLanguageServer
    (
        ILogger<GengoraLanguageServer> logger,
        GeneratorOrchestrator orchestrator
    )
    {
        this._Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this._Orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));

        this._Orchestrator.StateChanged += this.OnStateChanged;
        this._Orchestrator.CompilationCompleted += this.OnCompilationCompleted;
        this._Orchestrator.FileEmitted += this.OnFileEmitted;
    }

    /// <summary>
    /// Starts The Language Server Using Stdin/Stdout.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        this._Logger.LogInformation("Starting Gengora Language Server");

        var inputStream = Console.OpenStandardInput();
        var outputStream = Console.OpenStandardOutput();

        // Configure JSON Formatter With Named Parameters (Required For LSP)
        var formatter = new SystemTextJsonFormatter();
        formatter.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

        // Configure JsonRpc With Header-Delimited Messages (LSP Standard)
        var handler = new HeaderDelimitedMessageHandler(outputStream, inputStream, formatter);

        this._Rpc = new JsonRpc(handler);
        this._Rpc.AddLocalRpcTarget(this, new JsonRpcTargetOptions
        {
            AllowNonPublicInvocation = false,
            UseSingleObjectParameterDeserialization = true
        });

        this._Rpc.StartListening();

        this._Logger.LogInformation("Language Server Started");

        try
        {
            await this._Rpc.Completion.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            this._Logger.LogInformation("Language Server Shutdown Requested");
        }
    }

    /// <summary>
    /// Handles Initialize Request.
    /// </summary>
    [JsonRpcMethod("initialize", UseSingleObjectParameterDeserialization = true)]
    public async Task<InitializeResult> InitializeAsync(InitializeParams @params, CancellationToken cancellationToken)
    {
        var rootPath = @params.EffectiveRootPath;

        this._Logger.LogInformation("Initialize Request: RootPath={RootPath}, RootUri={RootUri}", @params.RootPath, @params.RootUri);

        if (this._IsInitialized)
        {
            throw new InvalidOperationException("Server Already Initialized");
        }

        if (String.IsNullOrEmpty(rootPath))
        {
            throw new ArgumentException("No Root Path Or Root URI Provided");
        }

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.3.0";

        // Initialize Orchestrator
        await this._Orchestrator.InitializeAsync(rootPath, cancellationToken);

        this._IsInitialized = true;

        return new InitializeResult
        {
            ServerInfo = new ServerInfo
            {
                Name = "Gengora Language Server",
                Version = version
            },
            Capabilities = new ServerCapabilities
            {
                StateNotifications = true,
                Diagnostics = true,
                FileWatching = true
            }
        };
    }

    /// <summary>
    /// Handles Shutdown Request.
    /// </summary>
    [JsonRpcMethod("shutdown")]
    public void Shutdown()
    {
        this._Logger.LogInformation("Shutdown Request");

        this._Orchestrator.Stop();
    }

    /// <summary>
    /// Handles Exit Notification.
    /// </summary>
    [JsonRpcMethod("exit")]
    public void Exit()
    {
        this._Logger.LogInformation("Exit Notification");

        this._Rpc?.Dispose();

        Environment.Exit(0);
    }

    /// <summary>
    /// Gets The Current Server State.
    /// </summary>
    [JsonRpcMethod("gengora/getState")]
    public GetStateResult GetState()
    {
        var project = this._Orchestrator.CurrentProject;

        return new GetStateResult
        {
            State = this._Orchestrator.CurrentState.ToString(),
            Project = project != null ? new ProjectInfoResult
            {
                Name = project.ProjectName,
                Path = project.ProjectPath,
                Directory = project.ProjectDirectory
            } : null
        };
    }

    /// <summary>
    /// Triggers Recompilation.
    /// </summary>
    [JsonRpcMethod("gengora/recompile")]
    public async Task<RecompileResult> RecompileAsync(CancellationToken cancellationToken)
    {
        this._Logger.LogInformation("Recompile Request");

        try
        {
            await this._Orchestrator.CompileAndExecuteAsync(cancellationToken);

            return new RecompileResult
            {
                Success = this._Orchestrator.CurrentState != GeneratorState.Error,
                Message = this._Orchestrator.CurrentState == GeneratorState.Error
                    ? "Compilation Failed"
                    : "Compilation Succeeded"
            };
        }
        catch (Exception ex)
        {
            this._Logger.LogError(ex, "Recompile Failed");

            return new RecompileResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// Stops The Generator.
    /// </summary>
    [JsonRpcMethod("gengora/stop")]
    public void Stop()
    {
        this._Logger.LogInformation("Stop Request");

        this._Orchestrator.Stop();
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        if (this._Rpc == null)
        {
            return;
        }

        var notification = new StateChangedNotification
        {
            State = e.NewState.ToString(),
            PreviousState = e.OldState.ToString(),
            Message = e.Message,
            Timestamp = DateTimeOffset.UtcNow.ToString("O")
        };

        _ = this._Rpc.NotifyAsync("gengora/stateChanged", notification);
    }

    private void OnCompilationCompleted(object? sender, CompilationResult e)
    {
        if (this._Rpc == null || e.Diagnostics.Count == 0)
        {
            return;
        }

        var notification = new DiagnosticsNotification
        {
            Diagnostics = e.Diagnostics.Select(d => new LspDiagnostic
            {
                Id = d.Id,
                Message = d.Message,
                Severity = d.Severity.ToString(),
                FilePath = d.FilePath,
                Line = d.Line,
                Column = d.Column
            }).ToList(),
            IsCompilationError = !e.Success
        };

        _ = this._Rpc.NotifyAsync("gengora/diagnostics", notification);
    }

    private void OnFileEmitted(object? sender, FileEmittedEventArgs e)
    {
        if (this._Rpc == null)
        {
            return;
        }

        var notification = new FileEmittedNotification
        {
            Path = e.FilePath,
            Timestamp = DateTimeOffset.UtcNow.ToString("O")
        };

        _ = this._Rpc.NotifyAsync("gengora/fileEmitted", notification);
    }

    private void ThrowIfDisposed()
    {
        if (this._IsDisposed)
        {
            throw new ObjectDisposedException(nameof(GengoraLanguageServer));
        }
    }

    public void Dispose()
    {
        if (!this._IsDisposed)
        {
            this._Orchestrator.StateChanged -= this.OnStateChanged;
            this._Orchestrator.CompilationCompleted -= this.OnCompilationCompleted;
            this._Orchestrator.FileEmitted -= this.OnFileEmitted;
            this._Rpc?.Dispose();
            this._IsDisposed = true;
        }
    }
}
