using CNC.Core.Axes;

namespace CNC.Core.Configuration;

/// <summary>
/// Static configuration of one axis. Lengths are in the machine's configured units,
/// velocities in units per minute and accelerations in units per second squared.
/// </summary>
public sealed record AxisConfiguration
{
    public Axis Axis { get; init; }

    public bool IsEnabled { get; init; } = true;

    /// <summary>Motor steps (or encoder counts) per unit of travel.</summary>
    public double StepsPerUnit { get; init; } = 400;

    /// <summary>Units per minute.</summary>
    public double MaxVelocity { get; init; } = 3000;

    /// <summary>Units per second squared.</summary>
    public double MaxAcceleration { get; init; } = 300;

    /// <summary>Lower software travel limit in machine coordinates.</summary>
    public double MinPosition { get; init; }

    /// <summary>Upper software travel limit in machine coordinates.</summary>
    public double MaxPosition { get; init; } = 300;

    public bool InvertDirection { get; init; }

    /// <summary>Direction the axis travels to find its home switch.</summary>
    public HomeDirection HomeDirection { get; init; } = HomeDirection.Negative;

    /// <summary>Machine coordinate assigned when the home switch is found.</summary>
    public double HomePosition { get; init; }

    /// <summary>Units per minute while searching for the home switch.</summary>
    public double HomingVelocity { get; init; } = 600;

    public double TravelLength => MaxPosition - MinPosition;
}

public enum HomeDirection
{
    Negative,
    Positive,
}
