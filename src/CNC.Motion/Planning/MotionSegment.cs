using CNC.Core.Geometry;

namespace CNC.Motion.Planning;

/// <summary>
/// One straight piece of planned motion. Arcs are approximated by several segments.
/// Speeds are in units per second, acceleration in units per second squared.
/// </summary>
public sealed record MotionSegment
{
    public required int Index { get; init; }

    public required Position Start { get; init; }

    public required Position End { get; init; }

    public required double Length { get; init; }

    /// <summary>Unit vector from <see cref="Start"/> to <see cref="End"/>.</summary>
    public required Position Direction { get; init; }

    public required MotionKind Kind { get; init; }

    /// <summary>Programmed feed rate in units per second; zero for rapids.</summary>
    public required double FeedRate { get; init; }

    /// <summary>Highest speed along this segment that keeps every axis within its velocity limit.</summary>
    public required double MaxSpeed { get; init; }

    /// <summary>Highest acceleration along this segment that keeps every axis within its acceleration limit.</summary>
    public required double Acceleration { get; init; }

    /// <summary>
    /// Highest speed allowed at the corner between this segment and the next, from geometry and axis
    /// limits only (feed rates are applied later). Zero for the last segment: the plan ends at rest.
    /// </summary>
    public required double JunctionSpeed { get; init; }

    public required int SourceLine { get; init; }

    /// <summary>Speed limit on this segment for the given feed override (rapids ignore the override).</summary>
    public double SpeedCap(double feedOverride) =>
        Kind == MotionKind.Rapid ? MaxSpeed : Math.Min(FeedRate * feedOverride, MaxSpeed);

    public Position PositionAt(double distance) => Start + (Direction * distance);
}
