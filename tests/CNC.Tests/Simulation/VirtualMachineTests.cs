using CNC.Core.Axes;
using CNC.Core.Geometry;
using CNC.Hardware.Motion;
using CNC.Motion.Planning;
using CNC.Simulation.Machine;
using CNC.Tests.TestSupport;

namespace CNC.Tests.Simulation;

public sealed class VirtualMachineTests
{
    private static readonly Position Target = new(100, 0, 0);

    [Fact]
    public void Execute_MovesOverTimeAndReportsCompletion()
    {
        var machine = new VirtualMachine(MotionTestData.Limits(), Position.Zero);
        Assert.True(machine.Execute(FeedTo(Target)).IsSuccess);

        machine.Advance(TimeSpan.FromMilliseconds(100));
        var moving = machine.GetStatus();
        Assert.Equal(MotionState.Running, moving.State);
        Assert.InRange(moving.MachinePosition.X, 0.1, 99);
        Assert.True(moving.FeedRate > 0);

        AdvanceUntilIdle(machine);
        Assert.Equal(Target, machine.Position);
        Assert.Equal(MotionState.Idle, machine.GetStatus().State);
        var completion = Assert.Single(machine.DrainEvents().Completions);
        Assert.Equal(MotionCompletion.Completed, completion.Completion);
    }

    [Fact]
    public void Execute_WhileMoving_IsRejected()
    {
        var machine = new VirtualMachine(MotionTestData.Limits(), Position.Zero);
        machine.Execute(FeedTo(Target));

        var second = machine.Execute(FeedTo(new Position(0, 50, 0)));

        Assert.True(second.IsFailure);
        Assert.Contains("already in progress", second.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_PlanNotStartingAtCurrentPosition_IsRejected()
    {
        var machine = new VirtualMachine(MotionTestData.Limits(), new Position(10, 0, 0));

        var result = machine.Execute(FeedTo(Target));

        Assert.True(result.IsFailure);
        Assert.Contains("plan starts at", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void EmergencyStop_HaltsInstantlyAndBlocksMotionUntilReset()
    {
        var machine = new VirtualMachine(MotionTestData.Limits(), Position.Zero);
        machine.Execute(FeedTo(Target));
        machine.Advance(TimeSpan.FromMilliseconds(200));
        var position = machine.Position;

        machine.EmergencyStop();
        machine.Advance(TimeSpan.FromSeconds(1));

        var status = machine.GetStatus();
        Assert.Equal(MotionState.EmergencyStop, status.State);
        Assert.Equal(position, status.MachinePosition);
        Assert.Equal(Position.Zero, status.Velocity);
        Assert.Equal(MotionCompletion.Aborted, Assert.Single(machine.DrainEvents().Completions).Completion);
        Assert.True(machine.Execute(FeedTo(Target, start: position)).IsFailure);

        Assert.True(machine.Reset().IsSuccess);
        Assert.Equal(MotionState.Idle, machine.GetStatus().State);
        Assert.True(machine.Execute(FeedTo(Target, start: position)).IsSuccess);
    }

    [Fact]
    public void EmergencyStop_WhenIdle_StillEntersEmergencyStop()
    {
        var machine = new VirtualMachine(MotionTestData.Limits(), Position.Zero);

        machine.EmergencyStop();

        Assert.Equal(MotionState.EmergencyStop, machine.GetStatus().State);
        Assert.Empty(machine.DrainEvents().Completions);
    }

    [Fact]
    public void FeedHoldAndResume_PauseMotionOnThePath()
    {
        var machine = new VirtualMachine(MotionTestData.Limits(), Position.Zero);
        machine.Execute(FeedTo(Target, feed: 3000));
        machine.Advance(TimeSpan.FromMilliseconds(500));

        Assert.True(machine.FeedHold().IsSuccess);
        machine.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Equal(MotionState.Held, machine.GetStatus().State);
        var held = machine.Position;
        machine.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(held, machine.Position);

        Assert.True(machine.Resume().IsSuccess);
        AdvanceUntilIdle(machine);
        Assert.Equal(Target, machine.Position);
    }

    [Fact]
    public void Resume_WithoutHold_IsRejected()
    {
        var machine = new VirtualMachine(MotionTestData.Limits(), Position.Zero);

        Assert.True(machine.Resume().IsFailure);
        Assert.True(machine.FeedHold().IsFailure);
    }

    [Fact]
    public void Stop_DeceleratesAndReportsStopped()
    {
        var machine = new VirtualMachine(MotionTestData.Limits(), Position.Zero);
        machine.Execute(FeedTo(Target, feed: 3000));
        machine.Advance(TimeSpan.FromMilliseconds(500));

        Assert.True(machine.Stop().IsSuccess);
        Assert.Equal(MotionState.Stopping, machine.GetStatus().State);
        AdvanceUntilIdle(machine);

        Assert.InRange(machine.Position.X, 20, 30);
        Assert.Equal(MotionCompletion.Stopped, Assert.Single(machine.DrainEvents().Completions).Completion);
    }

    [Fact]
    public void LeavingTravelLimits_RaisesFaultAndAbortsMotion()
    {
        var machine = new VirtualMachine(MotionTestData.Limits(), Position.Zero);
        machine.Execute(FeedTo(Target, feed: 6000));
        machine.UpdateLimits(MotionTestData.Limits(max: 10));

        AdvanceUntilIdle(machine);

        var status = machine.GetStatus();
        Assert.Equal(MotionState.Fault, status.State);
        // Detected on the first 10 ms step past the limit; at 100 units/s that is at most 1 unit beyond it.
        Assert.InRange(status.MachinePosition.X, 10, 11.01);
        var (completions, faults) = machine.DrainEvents();
        Assert.Equal(MotionCompletion.Faulted, Assert.Single(completions).Completion);
        var fault = Assert.Single(faults).Fault;
        Assert.Equal(MotionFaultKind.SoftLimit, fault.Kind);
        Assert.Equal(Axis.X, fault.Axis);
    }

    [Fact]
    public void FeedOverride_IsValidatedAndApplied()
    {
        var machine = new VirtualMachine(MotionTestData.Limits(), Position.Zero);

        Assert.True(machine.SetFeedOverride(2.5).IsFailure);
        Assert.True(machine.SetFeedOverride(0.5).IsSuccess);
        machine.Execute(FeedTo(new Position(500, 0, 0), feed: 3000));
        machine.Advance(TimeSpan.FromSeconds(1));

        var status = machine.GetStatus();
        Assert.Equal(0.5, status.FeedOverride);
        Assert.Equal(1500, status.FeedRate, 3);
    }

    private static MotionPlan FeedTo(Position target, double feed = 6000, Position? start = null) =>
        MotionTestData.Plan(start ?? Position.Zero, LinearMotion.Feed(target, feed, 1));

    private static void AdvanceUntilIdle(VirtualMachine machine)
    {
        for (var i = 0; i < 100_000 && machine.GetStatus().IsBusy; i++)
        {
            machine.Advance(TimeSpan.FromMilliseconds(10));
        }

        Assert.False(machine.GetStatus().IsBusy, "Motion did not finish.");
    }
}
