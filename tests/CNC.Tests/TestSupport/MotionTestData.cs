using CNC.Core.Axes;
using CNC.Core.Geometry;
using CNC.Motion.Execution;
using CNC.Motion.Limits;
using CNC.Motion.Planning;

namespace CNC.Tests.TestSupport;

internal static class MotionTestData
{
    public const double Acceleration = 1000;
    public const double MaxSpeed = 100;

    /// <summary>All axes: ±1000 travel, 100 units/s (6000 units/min), 1000 units/s².</summary>
    public static MotionLimits Limits(
        double maxSpeed = MaxSpeed,
        double acceleration = Acceleration,
        double min = -1000,
        double max = 1000) =>
        new(
            new AxisLimits(Axis.X, true, maxSpeed, acceleration, min, max),
            new AxisLimits(Axis.Y, true, maxSpeed, acceleration, min, max),
            new AxisLimits(Axis.Z, true, maxSpeed, acceleration, min, max));

    public static MotionPlanner Planner(PlannerOptions? options = null) => new(options ?? new PlannerOptions());

    public static MotionPlan Plan(Position start, params MotionRequest[] requests) =>
        Plan(Limits(), start, requests);

    public static MotionPlan Plan(MotionLimits limits, Position start, params MotionRequest[] requests)
    {
        var result = Planner().Plan(limits, start, requests);
        Assert.True(result.IsSuccess, result.Error);
        return result.Value;
    }

    /// <summary>The 50 x 50 square from the specification, starting and ending at the origin, at Z = 0.</summary>
    public static MotionRequest[] Square(double feed = 3000) =>
    [
        LinearMotion.Feed(new Position(50, 0, 0), feed, 1),
        LinearMotion.Feed(new Position(50, 50, 0), feed, 2),
        LinearMotion.Feed(new Position(0, 50, 0), feed, 3),
        LinearMotion.Feed(new Position(0, 0, 0), feed, 4),
    ];

    /// <summary>Advances in fixed steps until finished; returns the simulated time used.</summary>
    public static TimeSpan RunToEnd(TrajectoryExecutor executor, TimeSpan? step = null, TimeSpan? timeout = null)
    {
        var dt = step ?? TimeSpan.FromMilliseconds(1);
        var limit = timeout ?? TimeSpan.FromMinutes(10);
        var elapsed = TimeSpan.Zero;
        while (!executor.IsFinished && elapsed < limit)
        {
            executor.Advance(dt);
            elapsed += dt;
        }

        Assert.True(executor.IsFinished, "The trajectory did not finish within the timeout.");
        return elapsed;
    }

    public static void AssertClose(Position expected, Position actual, double tolerance = 1e-6)
    {
        Assert.True(
            expected.DistanceTo(actual) <= tolerance,
            $"Expected {expected} but was {actual} (distance {expected.DistanceTo(actual)}).");
    }
}
