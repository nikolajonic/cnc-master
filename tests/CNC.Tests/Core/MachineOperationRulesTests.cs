using CNC.Core.Machine;

namespace CNC.Tests.Core;

public sealed class MachineOperationRulesTests
{
    [Theory]
    [InlineData(MachineState.EmergencyStop)]
    [InlineData(MachineState.Alarm)]
    [InlineData(MachineState.Disconnected)]
    [InlineData(MachineState.Running)]
    [InlineData(MachineState.Paused)]
    [InlineData(MachineState.Homing)]
    public void StartProgram_IsBlocked(MachineState state)
    {
        Assert.False(MachineOperationRules.IsAllowed(state, MachineOperation.StartProgram));
    }

    [Theory]
    [InlineData(MachineState.Idle)]
    [InlineData(MachineState.Ready)]
    public void StartProgram_IsAllowedWhenIdle(MachineState state)
    {
        Assert.True(MachineOperationRules.IsAllowed(state, MachineOperation.StartProgram));
    }

    [Theory]
    [InlineData(MachineState.EmergencyStop)]
    [InlineData(MachineState.Alarm)]
    [InlineData(MachineState.Running)]
    [InlineData(MachineState.Disconnected)]
    public void Jog_IsBlocked(MachineState state)
    {
        Assert.False(MachineOperationRules.IsAllowed(state, MachineOperation.Jog));
    }

    [Theory]
    [MemberData(nameof(MachineStateMachineTests.AllStates), MemberType = typeof(MachineStateMachineTests))]
    public void EmergencyStop_IsAlwaysAllowed(MachineState state)
    {
        Assert.True(MachineOperationRules.IsAllowed(state, MachineOperation.EmergencyStop));
    }

    [Theory]
    [MemberData(nameof(MachineStateMachineTests.AllStates), MemberType = typeof(MachineStateMachineTests))]
    public void Reset_IsOnlyAllowedFromEmergencyStopOrAlarm(MachineState state)
    {
        Assert.Equal(
            state is MachineState.EmergencyStop or MachineState.Alarm,
            MachineOperationRules.IsAllowed(state, MachineOperation.Reset));
    }

    [Theory]
    [InlineData(MachineState.Running)]
    [InlineData(MachineState.Homing)]
    [InlineData(MachineState.Jogging)]
    public void EditConfiguration_IsBlockedWhileMoving(MachineState state)
    {
        Assert.False(MachineOperationRules.IsAllowed(state, MachineOperation.EditConfiguration));
    }

    [Fact]
    public void Check_ExplainsRejection()
    {
        var result = MachineOperationRules.Check(MachineState.EmergencyStop, MachineOperation.StartProgram);

        Assert.True(result.IsFailure);
        Assert.Contains("EmergencyStop", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void StateMachine_CanPerform_UsesCurrentState()
    {
        var machine = new MachineStateMachine(MachineState.Idle);
        Assert.True(machine.CanPerform(MachineOperation.StartProgram).IsSuccess);

        machine.TryTransitionTo(MachineState.Running, "start");

        Assert.True(machine.CanPerform(MachineOperation.StartProgram).IsFailure);
        Assert.True(machine.CanPerform(MachineOperation.PauseProgram).IsSuccess);
    }
}
