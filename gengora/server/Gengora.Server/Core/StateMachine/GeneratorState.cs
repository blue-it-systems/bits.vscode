namespace Gengora.Server.Core.StateMachine;

/// <summary>
/// Represents The Possible States Of The Generator System.
/// See Specification R2.* For State Definitions.
/// </summary>
public enum GeneratorState
{
    /// <summary>
    /// Extension Activated, Waiting For Generator Discovery.
    /// </summary>
    Idle,

    /// <summary>
    /// Marker Detected In Workspace, Ready To Compile.
    /// </summary>
    GeneratorFound,

    /// <summary>
    /// Build Process Active, Assembly Generation In Progress.
    /// </summary>
    Compiling,

    /// <summary>
    /// Compiled Assembly Available, Ready To Execute.
    /// </summary>
    Ready,

    /// <summary>
    /// Generator Process Actively Executing.
    /// </summary>
    Running,

    /// <summary>
    /// Compilation Or Execution Failure Detected.
    /// </summary>
    Error,

    /// <summary>
    /// User Manually Stopped Generator Or Auto-Stopped After Completion.
    /// </summary>
    Stopped
}
