using CNC.Core.Axes;
using CNC.Core.Units;

namespace CNC.Core.Configuration;

/// <summary>
/// Immutable machine description. Edit with <c>with</c> expressions and persist through
/// <see cref="IMachineConfigurationService"/>, which validates before saving.
/// </summary>
public sealed record MachineConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string Name { get; init; } = "CNC Machine";

    /// <summary>Units used for every length, velocity and acceleration in this configuration.</summary>
    public MeasurementSystem Units { get; init; } = MeasurementSystem.Metric;

    public IReadOnlyList<AxisConfiguration> Axes { get; init; } = [];

    public SpindleConfiguration Spindle { get; init; } = new();

    public AxisConfiguration GetAxis(Axis axis) =>
        Axes.FirstOrDefault(a => a.Axis == axis)
        ?? throw new KeyNotFoundException($"Axis {axis} is not configured.");

    public MachineConfiguration WithAxis(AxisConfiguration axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return this with { Axes = [.. Axes.Where(a => a.Axis != axis.Axis).Append(axis).OrderBy(a => a.Axis)] };
    }

    /// <summary>A 600 x 400 x 150 mm router; safe, conservative values for simulation.</summary>
    public static MachineConfiguration CreateDefault() => new()
    {
        Name = "Default Router",
        Units = MeasurementSystem.Metric,
        Axes =
        [
            new AxisConfiguration
            {
                Axis = Axis.X,
                StepsPerUnit = 400,
                MaxVelocity = 5000,
                MaxAcceleration = 500,
                MinPosition = 0,
                MaxPosition = 600,
                HomeDirection = HomeDirection.Negative,
                HomePosition = 0,
                HomingVelocity = 1000,
            },
            new AxisConfiguration
            {
                Axis = Axis.Y,
                StepsPerUnit = 400,
                MaxVelocity = 5000,
                MaxAcceleration = 500,
                MinPosition = 0,
                MaxPosition = 400,
                HomeDirection = HomeDirection.Negative,
                HomePosition = 0,
                HomingVelocity = 1000,
            },
            new AxisConfiguration
            {
                Axis = Axis.Z,
                StepsPerUnit = 800,
                MaxVelocity = 2000,
                MaxAcceleration = 300,
                MinPosition = -150,
                MaxPosition = 0,
                HomeDirection = HomeDirection.Positive,
                HomePosition = 0,
                HomingVelocity = 500,
            },
        ],
        Spindle = new SpindleConfiguration
        {
            MinRpm = 0,
            MaxRpm = 24000,
            Acceleration = 4000,
            SupportsReverse = true,
        },
    };
}
