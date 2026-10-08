using CNC.Core.Axes;
using CNC.Core.Geometry;

namespace CNC.Motion.Interpolation;

/// <summary>
/// Approximates a circular or helical arc by chords whose distance from the true arc never exceeds
/// the tolerance. The normal axis (and any radius difference between start and end) is interpolated
/// linearly with the angle, so the last point is exactly the requested target.
/// </summary>
public static class ArcInterpolator
{
    private const double MaxChordAngle = Math.PI / 8;
    private const double AngleEpsilon = 1e-9;

    /// <summary>Points after <paramref name="start"/> up to and including <paramref name="target"/>.</summary>
    public static IReadOnlyList<Position> Interpolate(
        Position start,
        Position target,
        Position center,
        Plane plane,
        ArcDirection direction,
        double tolerance,
        int maxSegments)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tolerance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSegments);

        var (first, second, normal) = plane.GetAxes();
        var startRadius = Radius(start, center, first, second);
        var endRadius = Radius(target, center, first, second);
        if (startRadius <= tolerance || endRadius <= tolerance)
        {
            throw new ArgumentException("The arc radius must be larger than the arc tolerance.");
        }

        var startAngle = Math.Atan2(start[second] - center[second], start[first] - center[first]);
        var endAngle = Math.Atan2(target[second] - center[second], target[first] - center[first]);
        var sweep = SweepAngle(startAngle, endAngle, direction);

        var radius = Math.Max(startRadius, endRadius);
        var chordAngle = tolerance >= radius ? MaxChordAngle : Math.Min(MaxChordAngle, 2 * Math.Acos(1 - (tolerance / radius)));
        var count = (int)Math.Clamp(Math.Ceiling(Math.Abs(sweep) / chordAngle), 1, maxSegments);

        var points = new Position[count];
        for (var k = 1; k <= count; k++)
        {
            if (k == count)
            {
                points[k - 1] = target;
                break;
            }

            var t = (double)k / count;
            var angle = startAngle + (sweep * t);
            var r = startRadius + ((endRadius - startRadius) * t);
            points[k - 1] = start
                .With(first, center[first] + (r * Math.Cos(angle)))
                .With(second, center[second] + (r * Math.Sin(angle)))
                .With(normal, start[normal] + ((target[normal] - start[normal]) * t));
        }

        return points;
    }

    /// <summary>
    /// Signed sweep from start to end: positive counter-clockwise, negative clockwise.
    /// Equal start and end angles describe a full circle.
    /// </summary>
    public static double SweepAngle(double startAngle, double endAngle, ArcDirection direction)
    {
        var sweep = endAngle - startAngle;
        if (direction == ArcDirection.CounterClockwise)
        {
            while (sweep <= AngleEpsilon)
            {
                sweep += 2 * Math.PI;
            }
        }
        else
        {
            while (sweep >= -AngleEpsilon)
            {
                sweep -= 2 * Math.PI;
            }
        }

        return sweep;
    }

    private static double Radius(Position point, Position center, Axis first, Axis second)
    {
        var d1 = point[first] - center[first];
        var d2 = point[second] - center[second];
        return Math.Sqrt((d1 * d1) + (d2 * d2));
    }
}
