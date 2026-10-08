using CNC.Core.Geometry;

namespace CNC.Motion.Planning;

public enum MotionKind
{
    /// <summary>Positioning move at the highest speed the axes allow (G0).</summary>
    Rapid,

    /// <summary>Cutting move at the programmed feed rate (G1/G2/G3).</summary>
    Feed,
}

/// <summary>
/// A requested move in machine coordinates. Requests are converted by the <see cref="MotionPlanner"/>
/// into velocity-planned segments. <see cref="SourceLine"/> links motion back to the program line (0 if none).
/// </summary>
public abstract record MotionRequest(Position Target, MotionKind Kind, double FeedRate, int SourceLine);

/// <summary>Straight move. <see cref="MotionRequest.FeedRate"/> is in units per minute and ignored for rapids.</summary>
public sealed record LinearMotion(Position Target, MotionKind Kind, double FeedRate, int SourceLine = 0)
    : MotionRequest(Target, Kind, FeedRate, SourceLine)
{
    public static LinearMotion Rapid(Position target, int sourceLine = 0) =>
        new(target, MotionKind.Rapid, 0, sourceLine);

    public static LinearMotion Feed(Position target, double feedRate, int sourceLine = 0) =>
        new(target, MotionKind.Feed, feedRate, sourceLine);
}

/// <summary>
/// Circular or helical move around <paramref name="Center"/> in <paramref name="Plane"/>.
/// If the target equals the start in the plane, a full circle is cut.
/// </summary>
public sealed record ArcMotion(
    Position Target,
    Position Center,
    Plane Plane,
    ArcDirection Direction,
    double FeedRate,
    int SourceLine = 0)
    : MotionRequest(Target, MotionKind.Feed, FeedRate, SourceLine);
