using CNC.Core.Axes;
using CNC.Core.Configuration;
using CNC.Core.Geometry;

namespace CNC.Motion.Limits;

/// <summary>Kinematic limits for all linear axes, derived from the machine configuration.</summary>
public sealed class MotionLimits
{
    /// <summary>Slack allowed when comparing positions against travel limits (floating point rounding).</summary>
    public const double PositionTolerance = 1e-6;

    private readonly AxisLimits[] _axes;

    public MotionLimits(AxisLimits x, AxisLimits y, AxisLimits z)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(z);
        if (x.Axis != Axis.X || y.Axis != Axis.Y || z.Axis != Axis.Z)
        {
            throw new ArgumentException("Axis limits must be supplied in X, Y, Z order.");
        }

        _axes = [x, y, z];
    }

    public AxisLimits this[Axis axis] => _axes[(int)axis];

    public IReadOnlyList<AxisLimits> Axes => _axes;

    public static MotionLimits FromConfiguration(MachineConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new MotionLimits(
            AxisLimits.FromConfiguration(configuration.GetAxis(Axis.X)),
            AxisLimits.FromConfiguration(configuration.GetAxis(Axis.Y)),
            AxisLimits.FromConfiguration(configuration.GetAxis(Axis.Z)));
    }

    /// <summary>The first axis whose software travel limit <paramref name="position"/> violates, if any.</summary>
    public AxisLimits? FindTravelViolation(Position position)
    {
        foreach (var limits in _axes)
        {
            if (!limits.IsWithinTravel(position[limits.Axis], PositionTolerance))
            {
                return limits;
            }
        }

        return null;
    }

    /// <summary>
    /// Highest path speed (units/s) along <paramref name="direction"/> (a unit vector) at which no axis
    /// exceeds its own velocity limit.
    /// </summary>
    public double MaxSpeedAlong(Position direction) => LimitAlong(direction, static a => a.MaxVelocity);

    /// <summary>Highest path acceleration (units/s²) along <paramref name="direction"/> that keeps every axis within its limit.</summary>
    public double MaxAccelerationAlong(Position direction) => LimitAlong(direction, static a => a.MaxAcceleration);

    private double LimitAlong(Position direction, Func<AxisLimits, double> selector)
    {
        var limit = double.PositiveInfinity;
        foreach (var axis in _axes)
        {
            var component = Math.Abs(direction[axis.Axis]);
            if (component > 1e-12)
            {
                limit = Math.Min(limit, selector(axis) / component);
            }
        }

        return limit;
    }
}
