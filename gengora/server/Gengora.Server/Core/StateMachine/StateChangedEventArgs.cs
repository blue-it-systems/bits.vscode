namespace Gengora.Server.Core.StateMachine;

/// <summary>
/// Event Arguments For State Change Events.
/// </summary>
public sealed class StateChangedEventArgs : EventArgs
{
    /// <summary>
    /// The Previous State Before Transition.
    /// </summary>
    public GeneratorState OldState { get; }

    /// <summary>
    /// The New Current State After Transition.
    /// </summary>
    public GeneratorState NewState { get; }

    /// <summary>
    /// Optional Error Message If Transitioning To Error State.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Timestamp When The State Change Occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    public StateChangedEventArgs
    (
        GeneratorState oldState,
        GeneratorState newState,
        string? message = null
    )
    {
        this.OldState = oldState;
        this.NewState = newState;
        this.Message = message;
        this.Timestamp = DateTimeOffset.UtcNow;
    }
}
