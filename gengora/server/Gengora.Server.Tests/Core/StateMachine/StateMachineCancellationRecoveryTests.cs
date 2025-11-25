namespace Gengora.Server.Tests.Core.StateMachine;

using Gengora.Server.Core.StateMachine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions;
using TUnit.Core;

/// <summary>
/// Tests For Bug Fix: Server Should Not Hang In Compiling State After Cancellation.
/// When A Cancellation Occurs During Compilation, The Server Should Recover To Ready State.
/// </summary>
public sealed class StateMachineCancellationRecoveryTests
{
    private readonly ILoggerFactory _LoggerFactory;

    public StateMachineCancellationRecoveryTests()
    {
        this._LoggerFactory = NullLoggerFactory.Instance;
    }

    /// <summary>
    /// Tests That State Machine Can Transition From Compiling Back To Ready.
    /// This Is The Recovery Path After Cancellation.
    /// </summary>
    [Test]
    public async Task Compiling_CanTransitionTo_Ready()
    {
        // Arrange
        var stateMachine = new GeneratorStateMachine(this._LoggerFactory.CreateLogger<GeneratorStateMachine>());
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);

        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Compiling);

        // Act - Transition Back To Ready (Simulates Cancellation Recovery)
        var result = stateMachine.TryTransition(GeneratorState.Ready);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Ready);
    }

    /// <summary>
    /// Tests That State Machine Can Transition From Compiling To Error.
    /// This Is Another Recovery Path.
    /// </summary>
    [Test]
    public async Task Compiling_CanTransitionTo_Error()
    {
        // Arrange
        var stateMachine = new GeneratorStateMachine(this._LoggerFactory.CreateLogger<GeneratorStateMachine>());
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);

        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Compiling);

        // Act - Transition To Error
        var result = stateMachine.TryTransition(GeneratorState.Error);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Error);
    }

    /// <summary>
    /// Tests That The State Changed Event Is Fired When Recovering From Compiling To Ready.
    /// </summary>
    [Test]
    public async Task Compiling_ToReady_RaisesStateChangedEvent()
    {
        // Arrange
        var stateMachine = new GeneratorStateMachine(this._LoggerFactory.CreateLogger<GeneratorStateMachine>());
        var stateChanges = new List<(GeneratorState From, GeneratorState To)>();

        stateMachine.StateChanged += (s, e) =>
        {
            stateChanges.Add((e.OldState, e.NewState));
        };

        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);

        // Act
        stateMachine.TryTransition(GeneratorState.Ready);

        // Assert - Should Have 3 State Changes
        await Assert.That(stateChanges.Count).IsEqualTo(3);
        await Assert.That(stateChanges[2].From).IsEqualTo(GeneratorState.Compiling);
        await Assert.That(stateChanges[2].To).IsEqualTo(GeneratorState.Ready);
    }

    /// <summary>
    /// Tests The Full Workflow: Idle → GeneratorFound → Compiling → Ready (Cancelled) → Ready Can Continue.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithCancellation_CanContinue()
    {
        // Arrange
        var stateMachine = new GeneratorStateMachine(this._LoggerFactory.CreateLogger<GeneratorStateMachine>());

        // Progress Through Normal Workflow
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Idle);

        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.GeneratorFound);

        stateMachine.TryTransition(GeneratorState.Compiling);
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Compiling);

        // Simulate Cancellation - Go Back To Ready
        stateMachine.TryTransition(GeneratorState.Ready);
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Ready);

        // Verify Server Can Continue - Running State Should Still Be Accessible
        stateMachine.TryTransition(GeneratorState.Running);
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Running);

        // And Back To Ready
        stateMachine.TryTransition(GeneratorState.Ready);
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Ready);
    }

    /// <summary>
    /// Tests That Reset From Compiling State Works.
    /// </summary>
    [Test]
    public async Task Reset_FromCompilingState_ReturnsToIdle()
    {
        // Arrange
        var stateMachine = new GeneratorStateMachine(this._LoggerFactory.CreateLogger<GeneratorStateMachine>());
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);

        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Compiling);

        // Act
        stateMachine.Reset();

        // Assert
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Idle);
    }

    /// <summary>
    /// Tests That Multiple Cancellations Are Handled Gracefully.
    /// </summary>
    [Test]
    public async Task MultipleCancellations_HandledGracefully()
    {
        // Arrange
        var stateMachine = new GeneratorStateMachine(this._LoggerFactory.CreateLogger<GeneratorStateMachine>());

        // First Workflow - Cancelled During Compiling
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);
        stateMachine.TryTransition(GeneratorState.Ready);

        // Second Workflow - Start Again From Ready
        stateMachine.TryTransition(GeneratorState.Running);
        stateMachine.TryTransition(GeneratorState.Ready);
        stateMachine.TryTransition(GeneratorState.Compiling);
        stateMachine.TryTransition(GeneratorState.Ready); // Cancelled Again

        // Third Workflow - Continue
        stateMachine.TryTransition(GeneratorState.Running);

        // Assert - Should Be In Running State
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Running);
    }
}
