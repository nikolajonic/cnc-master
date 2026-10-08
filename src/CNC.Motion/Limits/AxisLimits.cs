using CNC.Core.Axes;
using CNC.Core.Configuration;

namespace CNC.Motion.Limits;

/// <summary>
/// Kinematic limits of one axis in the units used by the motion engine: lengths in machine units,
/// velocity in units per second and acceleration in units per second squared.
/// </summary>
public sealed record AxisLimits(
    Axis Axis,
    bool IsEnabled,
    double MaxVelocity,
    double MaxAcceleration,
    double MinPosition,
    double MaxPosition)
{
    public static AxisLimits FromConfiguration(AxisConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new AxisLimits(
            configuration.Axis,
            configuration.IsEnabled,
            configuration.MaxVelocity / 60.0,
            configuration.MaxAcceleration,
            configuration.MinPosition,
            configuration.MaxPosition);
    }

    public bool IsWithinTravel(double position, double tolerance) =>
        position >= MinPosition - tolerance && position <= MaxPosition + tolerance;
}
