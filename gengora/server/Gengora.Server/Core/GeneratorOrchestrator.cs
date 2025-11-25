namespace Gengora.Server.Core;

using Gengora.Server.Core.Compilation;
using Gengora.Server.Core.Discovery;
using Gengora.Server.Core.Execution;
using Gengora.Server.Core.FileWatching;
using Gengora.Server.Core.Messaging;
using Gengora.Server.Core.StateMachine;
using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates The Generator Lifecycle.
/// Coordinates Discovery, Compilation, Execution, And File Watching.
/// </summary>
public sealed class GeneratorOrchestrator : IDisposable
{
    private readonly ILogger<GeneratorOrchestrator> _Logger;
    private readonly ProjectMarkerScanner _ProjectScanner;
    private readonly FileWatcherService _FileWatcher;
    private readonly DotnetCompilationService _CompilationService;
    private readonly GeneratorExecutor _Executor;
    private readonly GeneratorStateMachine _StateMachine;
    private readonly ILoggerFactory _LoggerFactory;

    private GeneratorProjectInfo? _CurrentProject;
    private string? _CurrentAssemblyPath;
    private bool _IsDisposed;
    private CancellationTokenSource? _WorkflowCts;

    /// <summary>
    /// Event Raised When State Changes.
    /// </summary>
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event Raised When A Generator Message Is Received.
    /// </summary>
    public event EventHandler<GeneratorMessage>? MessageReceived;

    /// <summary>
    /// Event Raised When A File Is Emitted.
    /// </summary>
    public event EventHandler<FileEmittedEventArgs>? FileEmitted;

    /// <summary>
    /// Event Raised When Compilation Diagnostics Are Available.
    /// </summary>
    public event EventHandler<CompilationResult>? CompilationCompleted;

    public GeneratorOrchestrator
    (
        ILogger<GeneratorOrchestrator> logger,
        ILoggerFactory loggerFactory,
        ProjectMarkerScanner projectScanner,
        FileWatcherService fileWatcher,
        DotnetCompilationService compilationService,
        GeneratorExecutor executor
    )
    {
        this._Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this._LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        this._ProjectScanner = projectScanner ?? throw new ArgumentNullException(nameof(projectScanner));
        this._FileWatcher = fileWatcher ?? throw new ArgumentNullException(nameof(fileWatcher));
        this._CompilationService = compilationService ?? throw new ArgumentNullException(nameof(compilationService));
        this._Executor = executor ?? throw new ArgumentNullException(nameof(executor));

        this._StateMachine = new GeneratorStateMachine(loggerFactory.CreateLogger<GeneratorStateMachine>());
        this._StateMachine.StateChanged += this.OnStateMachineStateChanged;
        this._FileWatcher.FileChanged += this.OnFileChanged;
        this._Executor.MessageReceived += this.OnMessageReceived;
        this._Executor.FileEmitted += this.OnFileEmitted;
    }

    /// <summary>
    /// Gets The Current State.
    /// </summary>
    public GeneratorState CurrentState => this._StateMachine.CurrentState;

    /// <summary>
    /// Gets The Currently Loaded Project (If Any).
    /// </summary>
    public GeneratorProjectInfo? CurrentProject => this._CurrentProject;

    /// <summary>
    /// Initializes The Orchestrator With A Workspace Root.
    /// Discovers And Loads Generator Projects.
    /// </summary>
    public async Task InitializeAsync(string workspaceRoot, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        this._Logger.LogInformation("Initializing Orchestrator For Workspace: {WorkspaceRoot}", workspaceRoot);

        // Discover Generator Projects
        var project = await this._ProjectScanner.ScanAsync(workspaceRoot, cancellationToken);

        if (project == null)
        {
            this._Logger.LogInformation("No Generator Projects Found In Workspace");

            return;
        }

        this._Logger.LogInformation("Found Generator Project: {ProjectName}", project.ProjectName);

        this._CurrentProject = project;
        this._StateMachine.TryTransition(GeneratorState.GeneratorFound);

        // Start File Watching
        this._FileWatcher.StartWatching(project);

        // Compile And Execute
        await this.CompileAndExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Compiles And Executes The Current Generator.
    /// </summary>
    public async Task CompileAndExecuteAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        if (this._CurrentProject == null)
        {
            this._Logger.LogWarning("No Generator Project Loaded");

            return;
        }

        // Cancel Any Previous Workflow
        this._WorkflowCts?.Cancel();
        this._WorkflowCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var token = this._WorkflowCts.Token;

        try
        {
            // Compile
            if (!this._StateMachine.TryTransition(GeneratorState.Compiling))
            {
                this._Logger.LogWarning("Cannot Transition To Compiling State");

                return;
            }

            var compilationResult = await this._CompilationService.CompileAsync(this._CurrentProject, token);

            this.CompilationCompleted?.Invoke(this, compilationResult);

            if (!compilationResult.Success)
            {
                this._StateMachine.TransitionToError($"Compilation Failed: {compilationResult.ErrorMessage}");

                return;
            }

            this._CurrentAssemblyPath = compilationResult.AssemblyPath;

            // Ready State
            if (!this._StateMachine.TryTransition(GeneratorState.Ready))
            {
                this._Logger.LogWarning("Cannot Transition To Ready State");

                return;
            }

            // Execute
            await this.ExecuteAsync(token);
        }
        catch (OperationCanceledException)
        {
            this._Logger.LogInformation("Workflow Cancelled");
        }
        catch (Exception ex)
        {
            this._Logger.LogError(ex, "Workflow Failed");

            this._StateMachine.TransitionToError(ex.Message);
        }
    }

    /// <summary>
    /// Executes The Current Generator Assembly.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        if (this._CurrentProject == null || String.IsNullOrEmpty(this._CurrentAssemblyPath))
        {
            this._Logger.LogWarning("No Generator Available For Execution");

            return;
        }

        try
        {
            if (!this._StateMachine.TryTransition(GeneratorState.Running))
            {
                this._Logger.LogWarning("Cannot Transition To Running State");

                return;
            }

            var result = await this._Executor.ExecuteAsync
            (
                this._CurrentAssemblyPath,
                this._CurrentProject,
                cancellationToken
            );

            if (result.Success)
            {
                // Back To Ready State After Successful Execution
                this._StateMachine.TryTransition(GeneratorState.Ready);
            }
            else
            {
                this._StateMachine.TransitionToError(result.ErrorMessage ?? "Execution Failed");
            }
        }
        catch (OperationCanceledException)
        {
            this._Logger.LogInformation("Execution Cancelled");

            this._StateMachine.TryTransition(GeneratorState.Ready);
        }
        catch (Exception ex)
        {
            this._Logger.LogError(ex, "Execution Failed");

            this._StateMachine.TransitionToError(ex.Message);
        }
    }

    /// <summary>
    /// Stops The Generator And Resets State.
    /// </summary>
    public void Stop()
    {
        this.ThrowIfDisposed();

        this._WorkflowCts?.Cancel();
        this._Executor.StopCurrentExecution();
        this._FileWatcher.StopWatching();
        this._StateMachine.TryTransition(GeneratorState.Stopped);

        this._Logger.LogInformation("Generator Stopped");
    }

    /// <summary>
    /// Resets The Orchestrator To Initial State.
    /// </summary>
    public void Reset()
    {
        this.ThrowIfDisposed();

        this._WorkflowCts?.Cancel();
        this._Executor.StopCurrentExecution();
        this._FileWatcher.StopWatching();
        this._StateMachine.Reset();

        this._CurrentProject = null;
        this._CurrentAssemblyPath = null;

        this._Logger.LogInformation("Orchestrator Reset");
    }

    private void OnStateMachineStateChanged(object? sender, StateChangedEventArgs e)
    {
        this._Logger.LogDebug("State Changed: {OldState} -> {NewState}", e.OldState, e.NewState);

        this.StateChanged?.Invoke(this, e);
    }

    private async void OnFileChanged(object? sender, FileChangedEventArgs e)
    {
        this._Logger.LogDebug("File Changed: {FilePath}", e.FilePath);

        // R3.3: Recompile On Source File Change
        try
        {
            await this.CompileAndExecuteAsync();
        }
        catch (Exception ex)
        {
            this._Logger.LogError(ex, "Failed To Handle File Change");
        }
    }

    private void OnMessageReceived(object? sender, GeneratorMessage e)
    {
        this.MessageReceived?.Invoke(this, e);
    }

    private void OnFileEmitted(object? sender, FileEmittedEventArgs e)
    {
        this.FileEmitted?.Invoke(this, e);
    }

    private void ThrowIfDisposed()
    {
        if (this._IsDisposed)
        {
            throw new ObjectDisposedException(nameof(GeneratorOrchestrator));
        }
    }

    public void Dispose()
    {
        if (!this._IsDisposed)
        {
            this._WorkflowCts?.Cancel();
            this._WorkflowCts?.Dispose();
            this._StateMachine.StateChanged -= this.OnStateMachineStateChanged;
            this._FileWatcher.FileChanged -= this.OnFileChanged;
            this._Executor.MessageReceived -= this.OnMessageReceived;
            this._Executor.FileEmitted -= this.OnFileEmitted;
            this._IsDisposed = true;
        }
    }
}
