using CNC.Core.Machine;

namespace CNC.Tests.Core;

public sealed class MachineStateMachineTests
{
    public static TheoryData<MachineState> AllStates()
    {
        var data = new TheoryData<MachineState>();
        foreach (var state in Enum.GetValues<MachineState>())
        {
            data.Add(state);
        }

        return data;
    }

    [Fact]
    public void InitialState_IsDisconnected()
    {
        Assert.Equal(MachineState.Disconnected, new MachineStateMachine().Current);
    }

    [Theory]
    [InlineData(MachineState.Disconnected, MachineState.Idle)]
    [InlineData(MachineState.Idle, MachineState.Running)]
    [InlineData(MachineState.Ready, MachineState.Running)]
    [InlineData(MachineState.Running, MachineState.Paused)]
    [InlineData(MachineState.Paused, MachineState.Running)]
    [InlineData(MachineState.Running, MachineState.Stopping)]
    [InlineData(MachineState.Paused, MachineState.Stopping)]
    [InlineData(MachineState.Stopping, MachineState.Idle)]
    [InlineData(MachineState.Running, MachineState.Ready)]
    [InlineData(MachineState.Idle, MachineState.Homing)]
    [InlineData(MachineState.Homing, MachineState.Ready)]
    [InlineData(MachineState.Idle, MachineState.Jogging)]
    [InlineData(MachineState.Jogging, MachineState.Idle)]
    [InlineData(MachineState.Running, MachineState.Alarm)]
    [InlineData(MachineState.Alarm, MachineState.Resetting)]
    [InlineData(MachineState.EmergencyStop, MachineState.Resetting)]
    [InlineData(MachineState.Resetting, MachineState.Idle)]
    [InlineData(MachineState.Resetting, MachineState.Alarm)]
    public void AllowedTransitions_Succeed(MachineState from, MachineState to)
    {
        var machine = new MachineStateMachine(from);

        var result = machine.TryTransitionTo(to, "test");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(to, machine.Current);
    }

    [Theory]
    [InlineData(MachineState.Disconnected, MachineState.Running)]
    [InlineData(MachineState.Disconnected, MachineState.Ready)]
    [InlineData(MachineState.Idle, MachineState.Ready)]
    [InlineData(MachineState.Idle, MachineState.Paused)]
    [InlineData(MachineState.Running, MachineState.Homing)]
    [InlineData(MachineState.Running, MachineState.Jogging)]
    [InlineData(MachineState.Paused, MachineState.Idle)]
    [InlineData(MachineState.EmergencyStop, MachineState.Idle)]
    [InlineData(MachineState.EmergencyStop, MachineState.Running)]
    [InlineData(MachineState.EmergencyStop, MachineState.Alarm)]
    [InlineData(MachineState.Alarm, MachineState.Idle)]
    [InlineData(MachineState.Alarm, MachineState.Running)]
    [InlineData(MachineState.Idle, MachineState.Resetting)]
    [InlineData(MachineState.Disconnected, MachineState.Alarm)]
    public void ForbiddenTransitions_AreRejectedAndStateIsUnchanged(MachineState from, MachineState to)
    {
        var machine = new MachineStateMachine(from);

        var result = machine.TryTransitionTo(to, "test");

        Assert.True(result.IsFailure);
        Assert.Equal(from, machine.Current);
    }

    [Theory]
    [MemberData(nameof(AllStates))]
    public void EmergencyStop_IsReachableFromEveryOtherState(MachineState from)
    {
        var machine = new MachineStateMachine(from);

        var result = machine.TryTransitionTo(MachineState.EmergencyStop, "operator");

        if (from == MachineState.EmergencyStop)
        {
            Assert.True(result.IsFailure);
        }
        else
        {
            Assert.True(result.IsSuccess);
        }

        Assert.Equal(MachineState.EmergencyStop, machine.Current);
    }

    [Theory]
    [MemberData(nameof(AllStates))]
    public void EmergencyStop_CanOnlyBeLeftThroughResetOrDisconnect(MachineState target)
    {
        var allowed = MachineStateTransitions.IsAllowed(MachineState.EmergencyStop, target);

        Assert.Equal(target is MachineState.Resetting or MachineState.Disconnected, allowed);
    }

    [Theory]
    [MemberData(nameof(AllStates))]
    public void TransitionToSameState_IsRejected(MachineState state)
    {
        Assert.False(MachineStateTransitions.IsAllowed(state, state));
    }

    [Fact]
    public void SuccessfulTransition_RaisesStateChangedWithDetails()
    {
        var machine = new MachineStateMachine(MachineState.Idle);
        MachineStateChangedEventArgs? raised = null;
        machine.StateChanged += (_, e) => raised = e;

        machine.TryTransitionTo(MachineState.Running, "Cycle start");

        Assert.NotNull(raised);
        Assert.Equal(MachineState.Idle, raised.Previous);
        Assert.Equal(MachineState.Running, raised.Current);
        Assert.Equal("Cycle start", raised.Reason);
    }

    [Fact]
    public void RejectedTransition_RaisesRejectedButNotStateChanged()
    {
        var machine = new MachineStateMachine(MachineState.EmergencyStop);
        var changed = false;
        MachineTransitionRejectedEventArgs? rejected = null;
        machine.StateChanged += (_, _) => changed = true;
        machine.TransitionRejected += (_, e) => rejected = e;

        machine.TryTransitionTo(MachineState.Running, "Cycle start");

        Assert.False(changed);
        Assert.NotNull(rejected);
        Assert.Equal(MachineState.EmergencyStop, rejected.Current);
        Assert.Equal(MachineState.Running, rejected.Requested);
    }

    [Fact]
    public void TryTransition_RequiresReason()
    {
        var machine = new MachineStateMachine(MachineState.Idle);

        Assert.Throws<ArgumentException>(() => machine.TryTransitionTo(MachineState.Running, " "));
    }

    [Fact]
    public void CanTransitionTo_DoesNotChangeState()
    {
        var machine = new MachineStateMachine(MachineState.Idle);

        Assert.True(machine.CanTransitionTo(MachineState.Running));
        Assert.Equal(MachineState.Idle, machine.Current);
    }

    [Fact]
    public void ConcurrentTransitions_AreDeliveredInOrder()
    {
        var machine = new MachineStateMachine(MachineState.Idle);
        var observed = new List<(MachineState Previous, MachineState Current)>();
        machine.StateChanged += (_, e) => observed.Add((e.Previous, e.Current));

        Parallel.For(0, 1000, i =>
        {
            machine.TryTransitionTo(MachineState.Jogging, "jog");
            machine.TryTransitionTo(MachineState.Idle, "jog done");
        });

        for (var i = 1; i < observed.Count; i++)
        {
            Assert.Equal(observed[i - 1].Current, observed[i].Previous);
        }
    }
}
