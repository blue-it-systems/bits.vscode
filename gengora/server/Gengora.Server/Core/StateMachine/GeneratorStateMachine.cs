namespace Gengora.Server.Core.StateMachine;

using Microsoft.Extensions.Logging;

/// <summary>
/// Manages State Transitions For The Generator System.
/// Implements Specification R2.* State Transition Rules.
/// </summary>
public sealed class GeneratorStateMachine
{
    private readonly ILogger<GeneratorStateMachine> _Logger;
    private readonly object _Lock = new();
    private GeneratorState _CurrentState = GeneratorState.Idle;
    private string? _LastErrorMessage;

    /// <summary>
    /// Event Raised When State Changes.
    /// </summary>
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Gets The Current State Of The Generator System.
    /// </summary>
    public GeneratorState CurrentState
    {
        get
        {
            lock (this._Lock)
            {
                return this._CurrentState;
            }
        }
    }

    /// <summary>
    /// Gets The Last Error Message If In Error State.
    /// </summary>
    public string? LastErrorMessage
    {
        get
        {
            lock (this._Lock)
            {
                return this._LastErrorMessage;
            }
        }
    }

    public GeneratorStateMachine(ILogger<GeneratorStateMachine> logger)
    {
        this._Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Attempts To Transition To The Specified State.
    /// Returns True If Transition Was Successful.
    /// </summary>
    public bool TryTransition(GeneratorState targetState, string? errorMessage = null)
    {
        lock (this._Lock)
        {
            if (!this.IsValidTransition(this._CurrentState, targetState))
            {
                this._Logger.LogWarning
                (
                    "Invalid State Transition Attempted: {CurrentState} -> {TargetState}",
                    this._CurrentState,
                    targetState
                );

                return false;
            }

            var previousState = this._CurrentState;
            this._CurrentState = targetState;

            if (targetState == GeneratorState.Error)
            {
                this._LastErrorMessage = errorMessage;
            }
            else
            {
                this._LastErrorMessage = null;
            }

            this._Logger.LogDebug
            (
                "State Transition: {PreviousState} -> {CurrentState}",
                previousState,
                targetState
            );

            this.OnStateChanged(new StateChangedEventArgs(previousState, targetState, errorMessage));

            return true;
        }
    }

    /// <summary>
    /// Forces Transition To Error State From Any Current State.
    /// Per R2.6: Any State -> Error When Compilation Or Execution Fails.
    /// </summary>
    public void TransitionToError(string errorMessage)
    {
        lock (this._Lock)
        {
            var previousState = this._CurrentState;
            this._CurrentState = GeneratorState.Error;
            this._LastErrorMessage = errorMessage;

            this._Logger.LogError
            (
                "Error State Transition From {PreviousState}: {ErrorMessage}",
                previousState,
                errorMessage
            );

            this.OnStateChanged(new StateChangedEventArgs(previousState, GeneratorState.Error, errorMessage));
        }
    }

    /// <summary>
    /// Resets The State Machine To Idle State.
    /// Used By Reset Extension Command (R10.3).
    /// </summary>
    public void Reset()
    {
        lock (this._Lock)
        {
            var previousState = this._CurrentState;
            this._CurrentState = GeneratorState.Idle;
            this._LastErrorMessage = null;

            this._Logger.LogInformation("State Machine Reset From {PreviousState} To Idle", previousState);

            this.OnStateChanged(new StateChangedEventArgs(previousState, GeneratorState.Idle));
        }
    }

    /// <summary>
    /// Validates If A Transition Is Allowed Per Specification R2.*.
    /// </summary>
    private bool IsValidTransition(GeneratorState from, GeneratorState to)
    {
        // R2.6: Any State -> Error Is Always Valid
        if (to == GeneratorState.Error)
        {
            return true;
        }

        // R2.9: Any State -> Stopped Is Always Valid (User Stop Command)
        if (to == GeneratorState.Stopped)
        {
            return true;
        }

        return (from, to) switch
        {
            // R2.1: Idle -> Generator Found
            (GeneratorState.Idle, GeneratorState.GeneratorFound) => true,

            // R2.2: Generator Found -> Compiling
            (GeneratorState.GeneratorFound, GeneratorState.Compiling) => true,

            // R2.3: Compiling -> Ready
            (GeneratorState.Compiling, GeneratorState.Ready) => true,

            // R2.4: Ready -> Running
            (GeneratorState.Ready, GeneratorState.Running) => true,

            // R2.5: Running -> Ready (Execution Completes Successfully)
            (GeneratorState.Running, GeneratorState.Ready) => true,

            // R2.8: Error -> Compiling (Retry)
            (GeneratorState.Error, GeneratorState.Compiling) => true,

            // R3.1: Ready -> Compiling (File Change Triggers Recompile)
            (GeneratorState.Ready, GeneratorState.Compiling) => true,

            // Stopped -> Idle (After Stop, Can Return To Idle For Re-Discovery)
            (GeneratorState.Stopped, GeneratorState.Idle) => true,

            // Stopped -> Compiling (User Manually Starts After Stop)
            (GeneratorState.Stopped, GeneratorState.Compiling) => true,

            // All Other Transitions Are Invalid
            _ => false
        };
    }

    private void OnStateChanged(StateChangedEventArgs e)
    {
        this.StateChanged?.Invoke(this, e);
    }
}
