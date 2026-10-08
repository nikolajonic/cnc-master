using CNC.Core.Geometry;
using CNC.Motion.Execution;
using CNC.Motion.Planning;
using CNC.Tests.TestSupport;

namespace CNC.Tests.Motion;

public sealed class TrajectoryExecutorTests
{
    private static readonly TimeSpan Millisecond = TimeSpan.FromMilliseconds(1);

    [Fact]
    public void Run_ReachesTargetExactlyAndStopsAtRest()
    {
        var target = new Position(123.456, -45.6, -7.89);
        var executor = new TrajectoryExecutor(MotionTestData.Plan(Position.Zero, LinearMotion.Feed(target, 3000)));

        MotionTestData.RunToEnd(executor);

        Assert.Equal(TrajectoryState.Completed, executor.State);
        Assert.Equal(target, executor.Position);
        Assert.Equal(0, executor.Speed);
        Assert.Equal(1, executor.Progress, 9);
    }

    [Fact]
    public void Motion_IsInterpolatedOverTime()
    {
        var target = new Position(100, 0, 0);
        var executor = new TrajectoryExecutor(MotionTestData.Plan(Position.Zero, LinearMotion.Feed(target, 600)));

        executor.Advance(TimeSpan.FromSeconds(1));

        // 10 units/s: roughly 10 units after one second, certainly not at the target.
        Assert.InRange(executor.Position.X, 9.9, 10.0);
        Assert.Equal(TrajectoryState.Running, executor.State);
        Assert.Equal(10, executor.Speed, 6);
    }

    [Fact]
    public void Speed_NeverExceedsCapAndAccelerationIsLimited()
    {
        var plan = MotionTestData.Plan(Position.Zero, MotionTestData.Square(feed: 4800));
        var executor = new TrajectoryExecutor(plan);
        var dt = Millisecond.TotalSeconds;
        var previous = 0.0;
        var travelled = 0.0;

        for (var i = 0; i < 60_000 && !executor.IsFinished; i++)
        {
            executor.Advance(Millisecond);
            Assert.True(executor.Speed >= 0 && executor.Speed <= 80 + 1e-9, $"Speed {executor.Speed} is outside 0..80 units/s.");
            Assert.True(
                Math.Abs(executor.Speed - previous) <= (MotionTestData.Acceleration * dt) + 1e-6,
                $"Speed jumped from {previous} to {executor.Speed}.");
            Assert.True(executor.DistanceTravelled >= travelled, "The tool moved backwards along the path.");
            previous = executor.Speed;
            travelled = executor.DistanceTravelled;
        }

        Assert.True(executor.IsFinished, "The trajectory stalled before the end of the path.");
        Assert.Equal(Position.Zero, executor.Position);
    }

    [Fact]
    public void Corners_AreTakenAtOrBelowJunctionSpeed()
    {
        var plan = MotionTestData.Plan(Position.Zero, MotionTestData.Square(feed: 4800));
        var executor = new TrajectoryExecutor(plan);
        var segment = 0;

        for (var i = 0; i < 60_000 && !executor.IsFinished; i++)
        {
            executor.Advance(Millisecond);
            if (executor.SegmentIndex != segment && executor.SegmentIndex < plan.Segments.Count)
            {
                var junction = plan.Segments[segment].JunctionSpeed;
                Assert.True(
                    executor.Speed <= junction + (MotionTestData.Acceleration * Millisecond.TotalSeconds),
                    $"Corner {segment} taken at {executor.Speed}, junction limit {junction}.");
                segment = executor.SegmentIndex;
            }
        }
    }

    [Fact]
    public void ExecutionTime_MatchesEstimate()
    {
        var plan = MotionTestData.Plan(
            Position.Zero,
            [.. MotionTestData.Square(), new ArcMotion(Position.Zero, new Position(10, 0, 0), Plane.XY, ArcDirection.Clockwise, 2400)]);
        var executor = new TrajectoryExecutor(plan);

        var actual = MotionTestData.RunToEnd(executor).TotalSeconds;
        var estimate = plan.EstimateDuration().TotalSeconds;

        Assert.InRange(actual, estimate * 0.99, estimate * 1.01);
    }

    [Fact]
    public void FeedHold_DeceleratesHoldsAndResumes()
    {
        var target = new Position(200, 0, 0);
        var executor = new TrajectoryExecutor(MotionTestData.Plan(Position.Zero, LinearMotion.Feed(target, 6000)));
        executor.Advance(TimeSpan.FromSeconds(0.5));
        var speedAtHold = executor.Speed;
        var positionAtHold = executor.Position.X;

        executor.Hold();
        executor.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(TrajectoryState.Held, executor.State);
        Assert.Equal(0, executor.Speed);
        var stoppingDistance = executor.Position.X - positionAtHold;
        Assert.Equal(speedAtHold * speedAtHold / (2 * MotionTestData.Acceleration), stoppingDistance, 2);

        var heldPosition = executor.Position;
        executor.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(heldPosition, executor.Position);

        executor.Resume();
        MotionTestData.RunToEnd(executor);
        Assert.Equal(TrajectoryState.Completed, executor.State);
        Assert.Equal(target, executor.Position);
    }

    [Fact]
    public void Stop_DeceleratesAndEndsEarly()
    {
        var executor = new TrajectoryExecutor(MotionTestData.Plan(Position.Zero, LinearMotion.Feed(new Position(200, 0, 0), 6000)));
        executor.Advance(TimeSpan.FromSeconds(0.5));

        executor.Stop();
        MotionTestData.RunToEnd(executor);

        Assert.Equal(TrajectoryState.Stopped, executor.State);
        Assert.InRange(executor.Position.X, 40, 60);
        Assert.Equal(0, executor.Speed);
    }

    [Fact]
    public void Abort_StopsInstantly()
    {
        var executor = new TrajectoryExecutor(MotionTestData.Plan(Position.Zero, LinearMotion.Feed(new Position(200, 0, 0), 6000)));
        executor.Advance(TimeSpan.FromSeconds(0.5));
        var position = executor.Position;

        executor.Abort();
        executor.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(TrajectoryState.Stopped, executor.State);
        Assert.Equal(0, executor.Speed);
        Assert.Equal(position, executor.Position);
    }

    [Fact]
    public void FeedOverride_ScalesFeedMovesWithinAccelerationLimits()
    {
        var executor = new TrajectoryExecutor(MotionTestData.Plan(Position.Zero, LinearMotion.Feed(new Position(500, 0, 0), 3000)));
        executor.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(50, executor.Speed, 6);

        executor.FeedOverride = 0.5;
        executor.Advance(Millisecond);
        Assert.Equal(50 - (MotionTestData.Acceleration * 0.001), executor.Speed, 6);

        executor.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(25, executor.Speed, 6);

        executor.FeedOverride = 1.5;
        executor.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(75, executor.Speed, 6);
    }

    [Fact]
    public void FeedOverride_DoesNotAffectRapids()
    {
        var executor = new TrajectoryExecutor(MotionTestData.Plan(Position.Zero, LinearMotion.Rapid(new Position(500, 0, 0))), feedOverride: 0.1);

        executor.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(MotionTestData.MaxSpeed, executor.Speed, 6);
    }

    [Fact]
    public void FeedOverride_CannotExceedAxisVelocity()
    {
        var executor = new TrajectoryExecutor(MotionTestData.Plan(Position.Zero, LinearMotion.Feed(new Position(500, 0, 0), 5400)), feedOverride: 2.0);

        executor.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(MotionTestData.MaxSpeed, executor.Speed, 6);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.01)]
    [InlineData(double.NaN)]
    public void FeedOverride_OutOfRange_Throws(double value)
    {
        var executor = new TrajectoryExecutor(MotionTestData.Plan(Position.Zero, LinearMotion.Feed(new Position(1, 0, 0), 600)));

        Assert.Throws<ArgumentOutOfRangeException>(() => executor.FeedOverride = value);
    }

    [Fact]
    public void SourceLine_FollowsTheActiveSegment()
    {
        var executor = new TrajectoryExecutor(MotionTestData.Plan(Position.Zero, MotionTestData.Square(feed: 6000)));
        var lines = new List<int>();

        for (var i = 0; i < 60_000 && !executor.IsFinished; i++)
        {
            if (lines.Count == 0 || lines[^1] != executor.SourceLine)
            {
                lines.Add(executor.SourceLine);
            }

            executor.Advance(Millisecond);
        }

        Assert.Equal([1, 2, 3, 4], lines);
    }

    [Fact]
    public void EmptyPlan_IsCompletedImmediately()
    {
        var executor = new TrajectoryExecutor(new MotionPlan(new Position(1, 2, 3), []));

        Assert.Equal(TrajectoryState.Completed, executor.State);
        Assert.Equal(new Position(1, 2, 3), executor.Position);
    }
}
