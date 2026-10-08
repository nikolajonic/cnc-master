namespace CNC.Core.Configuration;

public sealed record SpindleConfiguration
{
    public double MinRpm { get; init; }

    public double MaxRpm { get; init; } = 24000;

    /// <summary>RPM per second, used for spin-up and spin-down.</summary>
    public double Acceleration { get; init; } = 4000;

    /// <summary>Whether the spindle can run counter-clockwise (M4).</summary>
    public bool SupportsReverse { get; init; } = true;
}
