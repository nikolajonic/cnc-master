using CNC.Core.Axes;
using CNC.Core.Geometry;
using CNC.Motion.Limits;
using CNC.Motion.Planning;
using CNC.Tests.TestSupport;

namespace CNC.Tests.Motion;

public sealed class MotionPlannerTests
{
    private static readonly Position Origin = Position.Zero;

    [Fact]
    public void FeedMove_ProducesOneSegmentEndingAtRest()
    {
        var plan = MotionTestData.Plan(Origin, LinearMotion.Feed(new Position(30, 40, 0), 600, sourceLine: 5));

        var segment = Assert.Single(plan.Segments);
        Assert.Equal(50, segment.Length, 9);
        Assert.Equal(new Position(0.6, 0.8, 0), segment.Direction);
        Assert.Equal(MotionKind.Feed, segment.Kind);
        Assert.Equal(10, segment.FeedRate, 9);
        Assert.Equal(0, segment.JunctionSpeed);
        Assert.Equal(5, segment.SourceLine);
        Assert.Equal(new Position(30, 40, 0), plan.End);
    }

    [Fact]
    public void DiagonalMove_IsLimitedByTheSlowestAxis()
    {
        var limits = new MotionLimits(
            new AxisLimits(Axis.X, true, MaxVelocity: 100, MaxAcceleration: 1000, -1000, 1000),
            new AxisLimits(Axis.Y, true, MaxVelocity: 50, MaxAcceleration: 200, -1000, 1000),
            new AxisLimits(Axis.Z, true, MaxVelocity: 10, MaxAcceleration: 100, -1000, 1000));

        var plan = MotionTestData.Plan(limits, Origin, LinearMotion.Rapid(new Position(100, 100, 0)));

        var segment = Assert.Single(plan.Segments);
        Assert.Equal(50 * Math.Sqrt(2), segment.MaxSpeed, 9);
        Assert.Equal(200 * Math.Sqrt(2), segment.Acceleration, 9);
        Assert.Equal(segment.MaxSpeed, segment.SpeedCap(1.0), 9);
    }

    [Fact]
    public void FeedRate_IsCappedByAxisVelocity()
    {
        var plan = MotionTestData.Plan(Origin, LinearMotion.Feed(new Position(100, 0, 0), feedRate: 60_000));

        Assert.Equal(MotionTestData.MaxSpeed, plan.Segments[0].SpeedCap(1.0), 9);
    }

    [Fact]
    public void CollinearSegments_JoinAtFullSpeed()
    {
        var plan = MotionTestData.Plan(
            Origin,
            LinearMotion.Feed(new Position(10, 0, 0), 600),
            LinearMotion.Feed(new Position(20, 0, 0), 600));

        Assert.Equal(MotionTestData.MaxSpeed, plan.Segments[0].JunctionSpeed, 9);
    }

    [Fact]
    public void RightAngleCorner_UsesJunctionDeviation()
    {
        var options = new PlannerOptions { JunctionDeviation = 0.01 };
        var result = MotionTestData.Planner(options).Plan(
            MotionTestData.Limits(),
            Origin,
            [LinearMotion.Feed(new Position(10, 0, 0), 600), LinearMotion.Feed(new Position(10, 10, 0), 600)]);

        var sinHalf = Math.Sqrt(0.5);
        var expected = Math.Sqrt(MotionTestData.Acceleration * 0.01 * sinHalf / (1 - sinHalf));
        Assert.Equal(expected, result.Value.Segments[0].JunctionSpeed, 9);
    }

    [Fact]
    public void Reversal_RequiresAFullStop()
    {
        var plan = MotionTestData.Plan(
            Origin,
            LinearMotion.Feed(new Position(10, 0, 0), 600),
            LinearMotion.Feed(new Position(0, 0, 0), 600));

        Assert.Equal(0, plan.Segments[0].JunctionSpeed);
    }

    [Fact]
    public void ZeroJunctionDeviation_StopsAtEveryCorner()
    {
        var result = MotionTestData.Planner(new PlannerOptions { JunctionDeviation = 0 })
            .Plan(MotionTestData.Limits(), Origin, MotionTestData.Square());

        Assert.All(result.Value.Segments, s => Assert.Equal(0, s.JunctionSpeed));
    }

    [Fact]
    public void TargetOutsideTravel_IsRejectedWithLineNumber()
    {
        var result = MotionTestData.Planner().Plan(
            MotionTestData.Limits(max: 100),
            Origin,
            [LinearMotion.Feed(new Position(150, 0, 0), 600, sourceLine: 7)]);

        Assert.True(result.IsFailure);
        Assert.Contains("Line 7", result.Error, StringComparison.Ordinal);
        Assert.Contains("X travel limits", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ArcBulgingOutsideTravel_IsRejected()
    {
        var result = MotionTestData.Planner().Plan(
            MotionTestData.Limits(min: 0, max: 100),
            Origin,
            [new ArcMotion(new Position(10, 0, 0), new Position(5, 0, 0), Plane.XY, ArcDirection.CounterClockwise, 600, 3)]);

        Assert.True(result.IsFailure);
        Assert.Contains("Y travel limits", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void StartOutsideTravel_IsRejected()
    {
        var result = MotionTestData.Planner().Plan(
            MotionTestData.Limits(min: 0, max: 100),
            new Position(-5, 0, 0),
            [LinearMotion.Rapid(new Position(10, 0, 0))]);

        Assert.True(result.IsFailure);
        Assert.Contains("current position", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void MovingADisabledAxis_IsRejected()
    {
        var enabled = MotionTestData.Limits();
        var limits = new MotionLimits(enabled[Axis.X], enabled[Axis.Y], enabled[Axis.Z] with { IsEnabled = false });

        var blocked = MotionTestData.Planner().Plan(limits, Origin, [LinearMotion.Rapid(new Position(0, 0, -5), 2)]);
        var allowed = MotionTestData.Planner().Plan(limits, Origin, [LinearMotion.Rapid(new Position(5, 5, 0), 3)]);

        Assert.True(blocked.IsFailure);
        Assert.Contains("Axis Z is disabled", blocked.Error, StringComparison.Ordinal);
        Assert.True(allowed.IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(double.NaN)]
    public void FeedMoveWithoutValidFeedRate_IsRejected(double feed)
    {
        var result = MotionTestData.Planner().Plan(MotionTestData.Limits(), Origin, [LinearMotion.Feed(new Position(1, 0, 0), feed, 4)]);

        Assert.True(result.IsFailure);
        Assert.Contains("feed rate", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroLengthMoves_AreDropped()
    {
        var plan = MotionTestData.Plan(Origin, LinearMotion.Rapid(Origin), LinearMotion.Feed(Origin, 100));

        Assert.True(plan.IsEmpty);
        Assert.Equal(TimeSpan.Zero, plan.EstimateDuration());
    }

    [Fact]
    public void Arc_IsSplitIntoChordsEndingAtTarget()
    {
        var target = new Position(0, 20, 0);
        var plan = MotionTestData.Plan(
            new Position(20, 0, 0),
            new ArcMotion(target, Origin, Plane.XY, ArcDirection.CounterClockwise, 1200, 9));

        Assert.True(plan.Segments.Count > 10);
        Assert.Equal(target, plan.End);
        Assert.All(plan.Segments, s => Assert.Equal(9, s.SourceLine));
        Assert.All(plan.Segments.SkipLast(1), s => Assert.True(s.JunctionSpeed > 0, "Arc chords should blend without stopping."));
        Assert.Equal(Math.PI * 10, plan.TotalLength, 1);
    }

    [Fact]
    public void Segments_AreContiguousAndIndexed()
    {
        var plan = MotionTestData.Plan(Origin, MotionTestData.Square());

        for (var i = 0; i < plan.Segments.Count; i++)
        {
            Assert.Equal(i, plan.Segments[i].Index);
            if (i > 0)
            {
                Assert.Equal(plan.Segments[i - 1].End, plan.Segments[i].Start);
            }
        }

        Assert.Equal(200, plan.TotalLength, 9);
    }

    [Fact]
    public void EstimateDuration_MatchesTrapezoidProfile()
    {
        var plan = MotionTestData.Plan(Origin, LinearMotion.Feed(new Position(100, 0, 0), feedRate: 600));

        // 10 units/s with 1000 units/s²: 0.01 s to accelerate, 0.01 s to stop, 99.9 units cruising.
        Assert.Equal(0.02 + 9.99, plan.EstimateDuration().TotalSeconds, 6);
        Assert.Equal(0.01 + 19.995, plan.EstimateDuration(0.5).TotalSeconds, 6);
    }

    [Fact]
    public void EstimateDuration_RejectsZeroOverride()
    {
        var plan = MotionTestData.Plan(Origin, LinearMotion.Feed(new Position(10, 0, 0), 600));

        Assert.Throws<ArgumentOutOfRangeException>(() => plan.EstimateDuration(0));
    }

    [Fact]
    public void RapidMoves_IgnoreFeedOverrideInEstimate()
    {
        var plan = MotionTestData.Plan(Origin, LinearMotion.Rapid(new Position(500, 0, 0)));

        Assert.Equal(plan.EstimateDuration(1.0), plan.EstimateDuration(0.25));
    }
}
