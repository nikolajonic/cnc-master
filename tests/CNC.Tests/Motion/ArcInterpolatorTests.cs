using CNC.Core.Geometry;
using CNC.Motion.Interpolation;

namespace CNC.Tests.Motion;

public sealed class ArcInterpolatorTests
{
    private const double Tolerance = 0.002;

    [Fact]
    public void QuarterCircleCounterClockwise_StaysOnRadiusAndEndsAtTarget()
    {
        var start = new Position(10, 0, 0);
        var target = new Position(0, 10, 0);

        var points = ArcInterpolator.Interpolate(start, target, Position.Zero, Plane.XY, ArcDirection.CounterClockwise, Tolerance, 10_000);

        Assert.Equal(target, points[^1]);
        Assert.All(points, p => Assert.Equal(10, Math.Sqrt((p.X * p.X) + (p.Y * p.Y)), 9));
        AssertMonotonic(points.Select(p => p.Y).Prepend(start.Y), increasing: true);
    }

    [Fact]
    public void QuarterCircleClockwise_TravelsTheShortWay()
    {
        var start = new Position(0, 10, 0);
        var target = new Position(10, 0, 0);

        var points = ArcInterpolator.Interpolate(start, target, Position.Zero, Plane.XY, ArcDirection.Clockwise, Tolerance, 10_000);

        Assert.All(points, p => Assert.True(p.X >= -1e-9 && p.Y >= -1e-9, $"{p} left the first quadrant."));
        AssertMonotonic(points.Select(p => p.X).Prepend(start.X), increasing: true);
    }

    [Fact]
    public void ChordError_StaysWithinTolerance()
    {
        var start = new Position(25, 0, 0);
        var points = ArcInterpolator.Interpolate(start, new Position(-25, 0, 0), Position.Zero, Plane.XY, ArcDirection.CounterClockwise, Tolerance, 10_000);

        var previous = start;
        foreach (var point in points)
        {
            var midpoint = (previous + point) / 2;
            var sagitta = 25 - Math.Sqrt((midpoint.X * midpoint.X) + (midpoint.Y * midpoint.Y));
            Assert.True(sagitta <= Tolerance + 1e-12, $"Chord error {sagitta} exceeds the tolerance.");
            previous = point;
        }
    }

    [Fact]
    public void SameStartAndEnd_CutsFullCircle()
    {
        var start = new Position(5, 0, 0);

        var points = ArcInterpolator.Interpolate(start, start, Position.Zero, Plane.XY, ArcDirection.Clockwise, Tolerance, 10_000);

        Assert.True(points.Count > 16);
        Assert.Equal(start, points[^1]);
        Assert.Contains(points, p => p.Y < -4.99);
        Assert.Contains(points, p => p.Y > 4.99);
    }

    [Fact]
    public void Helix_InterpolatesNormalAxisLinearly()
    {
        var start = new Position(5, 0, 0);
        var target = new Position(5, 0, -4);

        var points = ArcInterpolator.Interpolate(start, target, Position.Zero, Plane.XY, ArcDirection.CounterClockwise, Tolerance, 10_000);

        var halfway = points[(points.Count / 2) - 1];
        Assert.Equal(-2, halfway.Z, 1);
        Assert.Equal(-4, points[^1].Z);
        AssertMonotonic(points.Select(p => p.Z).Prepend(0), increasing: false);
    }

    [Fact]
    public void XzPlane_UsesZxBasisAndKeepsYConstant()
    {
        var start = new Position(10, 3, 0);
        var target = new Position(0, 3, 10);
        var center = new Position(0, 3, 0);

        var points = ArcInterpolator.Interpolate(start, target, center, Plane.XZ, ArcDirection.Clockwise, Tolerance, 10_000);

        Assert.All(points, p => Assert.Equal(3, p.Y, 12));
        Assert.All(points, p => Assert.Equal(10, Math.Sqrt((p.X * p.X) + (p.Z * p.Z)), 9));
        Assert.All(points, p => Assert.True(p.X >= -1e-9 && p.Z >= -1e-9, $"{p} took the long way round."));
    }

    [Fact]
    public void ZeroRadius_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ArcInterpolator.Interpolate(Position.Zero, new Position(1, 0, 0), Position.Zero, Plane.XY, ArcDirection.Clockwise, Tolerance, 100));
    }

    [Theory]
    [InlineData(0, Math.PI / 2, ArcDirection.CounterClockwise, Math.PI / 2)]
    [InlineData(0, Math.PI / 2, ArcDirection.Clockwise, -3 * Math.PI / 2)]
    [InlineData(Math.PI / 2, 0, ArcDirection.Clockwise, -Math.PI / 2)]
    [InlineData(1, 1, ArcDirection.CounterClockwise, 2 * Math.PI)]
    [InlineData(1, 1, ArcDirection.Clockwise, -2 * Math.PI)]
    public void SweepAngle_FollowsDirection(double start, double end, ArcDirection direction, double expected)
    {
        Assert.Equal(expected, ArcInterpolator.SweepAngle(start, end, direction), 9);
    }

    private static void AssertMonotonic(IEnumerable<double> values, bool increasing)
    {
        var list = values.ToList();
        for (var i = 1; i < list.Count; i++)
        {
            Assert.True(increasing ? list[i] >= list[i - 1] - 1e-12 : list[i] <= list[i - 1] + 1e-12, $"Not monotonic at index {i}.");
        }
    }
}
