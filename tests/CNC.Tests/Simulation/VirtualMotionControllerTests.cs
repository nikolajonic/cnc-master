using CNC.Core.Configuration;
using CNC.Core.Geometry;
using CNC.GCode;
using CNC.GCode.Commands;
using CNC.Hardware.Motion;
using CNC.Motion.Limits;
using CNC.Motion.Planning;
using CNC.Simulation;
using CNC.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace CNC.Tests.Simulation;

public sealed class VirtualMotionControllerTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task Connect_ReportsSimulationModeAndIdle()
    {
        using var controller = CreateController();
        Assert.Equal(MotionState.Disconnected, controller.Status.State);

        var result = await controller.ConnectAsync();

        Assert.True(result.IsSuccess);
        Assert.True(controller.IsConnected);
        Assert.Equal(ControllerMode.Simulation, controller.Mode);
        Assert.Equal(MotionState.Idle, controller.Status.State);
    }

    [Fact]
    public void Commands_WhenDisconnected_AreRejected()
    {
        using var controller = CreateController();

        Assert.True(controller.Execute(PlanFrom(controller, LinearMotion.Rapid(new Position(10, 10, 0)))).IsFailure);
        Assert.True(controller.FeedHold().IsFailure);
    }

    [Fact]
    public void EmergencyStop_IsAcceptedEvenWhenDisconnected()
    {
        using var controller = CreateController();

        controller.EmergencyStop();

        Assert.Equal(MotionState.EmergencyStop, controller.Status.State);
    }

    [Fact]
    public async Task Execute_RaisesStatusUpdatesAndCompletion()
    {
        using var controller = CreateController();
        await controller.ConnectAsync();
        var statuses = new List<MotionStatus>();
        var completions = new List<MotionCompletedEventArgs>();
        controller.StatusChanged += (_, e) => statuses.Add(e.Status);
        controller.MotionCompleted += (_, e) => completions.Add(e);

        var target = new Position(100, 50, -10);
        Assert.True(controller.Execute(PlanFrom(controller, LinearMotion.Feed(target, 3000, 1))).IsSuccess);
        StepUntilIdle(controller);

        Assert.Equal(target, controller.Status.MachinePosition);
        Assert.Contains(statuses, s => s.State == MotionState.Running);
        Assert.True(statuses.Count(s => s.State == MotionState.Running) > 10, "Expected periodic updates while moving.");
        Assert.Equal(MotionState.Idle, statuses[^1].State);
        Assert.Equal(MotionCompletion.Completed, Assert.Single(completions).Completion);
    }

    [Fact]
    public async Task EmergencyStop_DuringMotion_AbortsAndRequiresReset()
    {
        using var controller = CreateController();
        await controller.ConnectAsync();
        var completions = new List<MotionCompletedEventArgs>();
        controller.MotionCompleted += (_, e) => completions.Add(e);
        controller.Execute(PlanFrom(controller, LinearMotion.Feed(new Position(300, 0, 0), 3000, 1)));
        for (var i = 0; i < 50; i++)
        {
            controller.Step(Tick);
        }

        controller.EmergencyStop();
        var stoppedAt = controller.Status.MachinePosition;
        controller.Step(TimeSpan.FromSeconds(1));

        Assert.Equal(stoppedAt, controller.Status.MachinePosition);
        Assert.Equal(MotionCompletion.Aborted, Assert.Single(completions).Completion);
        Assert.True(controller.Execute(PlanFrom(controller, LinearMotion.Rapid(new Position(10, 10, 0)))).IsFailure);

        Assert.True(controller.Reset().IsSuccess);
        Assert.True(controller.Execute(PlanFrom(controller, LinearMotion.Rapid(new Position(10, 10, 0)))).IsSuccess);
    }

    [Fact]
    public async Task Disconnect_AbortsMotion()
    {
        using var controller = CreateController();
        await controller.ConnectAsync();
        var completions = new List<MotionCompletedEventArgs>();
        controller.MotionCompleted += (_, e) => completions.Add(e);
        controller.Execute(PlanFrom(controller, LinearMotion.Feed(new Position(300, 0, 0), 3000)));
        controller.Step(Tick);

        await controller.DisconnectAsync();

        Assert.False(controller.IsConnected);
        Assert.Equal(MotionState.Disconnected, controller.Status.State);
        Assert.Equal(MotionCompletion.Aborted, Assert.Single(completions).Completion);
    }

    [Fact]
    public async Task RealTimeLoop_MovesTheMachine()
    {
        var options = new VirtualMotionControllerOptions { RunRealTimeLoop = true, TickInterval = TimeSpan.FromMilliseconds(5) };
        await using var controller = CreateController(options, new CNC.Core.Abstractions.SystemClock());
        await controller.ConnectAsync();
        var done = new TaskCompletionSource<MotionCompletion>(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.MotionCompleted += (_, e) => done.TrySetResult(e.Completion);

        controller.Execute(PlanFrom(controller, LinearMotion.Rapid(new Position(5, 5, 0))));

        Assert.Equal(MotionCompletion.Completed, await done.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(new Position(5, 5, 0), controller.Status.MachinePosition);
        await controller.DisconnectAsync();
    }

    /// <summary>
    /// Integration: the example program from the specification is parsed, converted to machine
    /// coordinates with a fixed work offset, planned, and executed by the virtual controller.
    /// </summary>
    [Fact]
    public async Task SquareProgram_RunsEndToEnd()
    {
        const string source = """
            G21
            G90
            G0 X0 Y0
            G0 Z5
            G1 Z-1 F100
            G1 X50 F500
            G1 Y50
            G1 X0
            G1 Y0
            G0 Z5
            M5
            M30
            """;
        var workOffset = new Position(100, 100, -50);
        var program = GCodeProgram.Parse(source);
        Assert.False(program.HasErrors);

        var requests = program.Commands.OfType<MotionCommand>().Select(c => ToRequest(c, workOffset)).ToList();
        var configuration = MachineConfiguration.CreateDefault();
        using var controller = CreateController(configuration: configuration);
        await controller.ConnectAsync();
        var planResult = new MotionPlanner(new PlannerOptions())
            .Plan(MotionLimits.FromConfiguration(configuration), controller.Status.MachinePosition, requests);
        Assert.True(planResult.IsSuccess, planResult.Error);
        var plan = planResult.Value;

        var visitedLines = new HashSet<int>();
        var minZ = double.MaxValue;
        controller.StatusChanged += (_, e) =>
        {
            visitedLines.Add(e.Status.ActiveSourceLine);
            minZ = Math.Min(minZ, e.Status.MachinePosition.Z);
        };

        Assert.True(controller.Execute(plan).IsSuccess);
        var simulated = StepUntilIdle(controller);

        Assert.Equal(new Position(100, 100, -45), controller.Status.MachinePosition);
        Assert.Equal(-51, minZ, 6);
        Assert.Superset(new HashSet<int> { 3, 5, 6, 7, 8, 9, 10 }, visitedLines);
        var estimate = plan.EstimateDuration();
        Assert.InRange(simulated.TotalSeconds, estimate.TotalSeconds * 0.99, estimate.TotalSeconds * 1.01);

        // 24 s for the square at 500 mm/min, 2.4 s for the plunge at 100 mm/min, plus rapids and ramps.
        Assert.InRange(estimate.TotalSeconds, 27.5, 30);
    }

    private static MotionRequest ToRequest(MotionCommand command, Position offset) => command switch
    {
        RapidMoveCommand rapid => LinearMotion.Rapid(rapid.Target + offset, rapid.LineNumber),
        LinearMoveCommand linear => LinearMotion.Feed(linear.Target + offset, linear.FeedRate, linear.LineNumber),
        ArcMoveCommand arc => new ArcMotion(arc.Target + offset, arc.Center + offset, arc.Plane, arc.Direction, arc.FeedRate, arc.LineNumber),
        _ => throw new NotSupportedException(command.GetType().Name),
    };

    private static VirtualMotionController CreateController(
        VirtualMotionControllerOptions? options = null,
        CNC.Core.Abstractions.IClock? clock = null,
        MachineConfiguration? configuration = null) =>
        new(
            new FakeMachineConfigurationService(configuration ?? MachineConfiguration.CreateDefault()),
            clock ?? new FakeClock(),
            NullLogger<VirtualMotionController>.Instance,
            options ?? new VirtualMotionControllerOptions { RunRealTimeLoop = false });

    private static MotionPlan PlanFrom(VirtualMotionController controller, params MotionRequest[] requests) =>
        MotionTestData.Plan(
            MotionLimits.FromConfiguration(MachineConfiguration.CreateDefault()),
            controller.Status.MachinePosition,
            requests);

    private static TimeSpan StepUntilIdle(VirtualMotionController controller)
    {
        var elapsed = TimeSpan.Zero;
        while (controller.Status.IsBusy && elapsed < TimeSpan.FromMinutes(5))
        {
            controller.Step(Tick);
            elapsed += Tick;
        }

        Assert.False(controller.Status.IsBusy, "Motion did not finish.");
        return elapsed;
    }
}
