namespace Gengora.Server.Tests.Core.StateMachine;

using Gengora.Server.Core.StateMachine;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions;
using TUnit.Core;

/// <summary>
/// Tests For GeneratorStateMachine.
/// Verifies All State Transition Rules Per Specification R2.*.
/// </summary>
public class GeneratorStateMachineTests
{
    private GeneratorStateMachine CreateStateMachine() => new(NullLogger<GeneratorStateMachine>.Instance);

    [Test]
    public async Task InitialState_ShouldBeIdle()
    {
        // Arrange & Act
        var stateMachine = this.CreateStateMachine();

        // Assert
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Idle);
    }

    [Test]
    public async Task TryTransition_FromIdle_ToGeneratorFound_ShouldSucceed()
    {
        // Arrange
        var stateMachine = this.CreateStateMachine();

        // Act
        var result = stateMachine.TryTransition(GeneratorState.GeneratorFound);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.GeneratorFound);
    }

    [Test]
    public async Task TryTransition_FromIdle_ToCompiling_ShouldFail()
    {
        // Arrange
        var stateMachine = this.CreateStateMachine();

        // Act
        var result = stateMachine.TryTransition(GeneratorState.Compiling);

        // Assert
        await Assert.That(result).IsFalse();
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Idle);
    }

    [Test]
    public async Task TryTransition_FromGeneratorFound_ToCompiling_ShouldSucceed()
    {
        // Arrange
        var stateMachine = this.CreateStateMachine();
        stateMachine.TryTransition(GeneratorState.GeneratorFound);

        // Act
        var result = stateMachine.TryTransition(GeneratorState.Compiling);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Compiling);
    }

    [Test]
    public async Task TryTransition_FromCompiling_ToReady_ShouldSucceed()
    {
        // Arrange
        var stateMachine = this.CreateStateMachine();
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);

        // Act
        var result = stateMachine.TryTransition(GeneratorState.Ready);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Ready);
    }

    [Test]
    public async Task TryTransition_FromReady_ToRunning_ShouldSucceed()
    {
        // Arrange
        var stateMachine = this.CreateStateMachine();
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);
        stateMachine.TryTransition(GeneratorState.Ready);

        // Act
        var result = stateMachine.TryTransition(GeneratorState.Running);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Running);
    }

    [Test]
    public async Task TryTransition_FromRunning_ToReady_ShouldSucceed()
    {
        // Arrange
        var stateMachine = this.CreateStateMachine();
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);
        stateMachine.TryTransition(GeneratorState.Ready);
        stateMachine.TryTransition(GeneratorState.Running);

        // Act
        var result = stateMachine.TryTransition(GeneratorState.Ready);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Ready);
    }

    [Test]
    public async Task TransitionToError_FromAnyState_ShouldSucceed()
    {
        // Arrange
        var stateMachine = this.CreateStateMachine();
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);

        // Act
        stateMachine.TransitionToError("Test Error Message");

        // Assert
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Error);
    }

    [Test]
    public async Task TryTransition_FromError_ToCompiling_ShouldSucceed()
    {
        // Arrange - R2.6: From Error State Can Retry Compilation
        var stateMachine = this.CreateStateMachine();
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TransitionToError("Test Error");

        // Act
        var result = stateMachine.TryTransition(GeneratorState.Compiling);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Compiling);
    }

    [Test]
    public async Task Reset_ShouldReturnToIdle()
    {
        // Arrange
        var stateMachine = this.CreateStateMachine();
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);
        stateMachine.TryTransition(GeneratorState.Ready);

        // Act
        stateMachine.Reset();

        // Assert
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Idle);
    }

    [Test]
    public async Task TryTransition_ToStopped_FromReady_ShouldSucceed()
    {
        // Arrange
        var stateMachine = this.CreateStateMachine();
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);
        stateMachine.TryTransition(GeneratorState.Ready);

        // Act
        var result = stateMachine.TryTransition(GeneratorState.Stopped);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Stopped);
    }

    [Test]
    public async Task StateChanged_Event_ShouldBeRaised()
    {
        // Arrange
        var stateMachine = this.CreateStateMachine();
        StateChangedEventArgs? eventArgs = null;

        stateMachine.StateChanged += (sender, e) => eventArgs = e;

        // Act
        stateMachine.TryTransition(GeneratorState.GeneratorFound);

        // Assert
        await Assert.That(eventArgs).IsNotNull();
        await Assert.That(eventArgs!.OldState).IsEqualTo(GeneratorState.Idle);
        await Assert.That(eventArgs.NewState).IsEqualTo(GeneratorState.GeneratorFound);
    }

    [Test]
    public async Task TryTransition_FromReady_ToCompiling_ForHotReload_ShouldSucceed()
    {
        // Arrange - R3.3: Ready State Can Transition To Compiling For Hot-Reload
        var stateMachine = this.CreateStateMachine();
        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        stateMachine.TryTransition(GeneratorState.Compiling);
        stateMachine.TryTransition(GeneratorState.Ready);

        // Act
        var result = stateMachine.TryTransition(GeneratorState.Compiling);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Compiling);
    }
}
