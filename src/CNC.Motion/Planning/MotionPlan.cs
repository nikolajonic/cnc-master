using CNC.Core.Geometry;
using CNC.Motion.Profiles;

namespace CNC.Motion.Planning;

/// <summary>
/// Immutable, validated sequence of segments in machine coordinates, starting and ending at rest.
/// Produced by <see cref="MotionPlanner"/> and consumed by motion controllers.
/// </summary>
public sealed class MotionPlan
{
    public MotionPlan(Position start, IReadOnlyList<MotionSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        Start = start;
        Segments = segments;
        End = segments.Count > 0 ? segments[^1].End : start;
        TotalLength = segments.Sum(static s => s.Length);
    }

    public Position Start { get; }

    public Position End { get; }

    public IReadOnlyList<MotionSegment> Segments { get; }

    public double TotalLength { get; }

    public bool IsEmpty => Segments.Count == 0;

    /// <summary>
    /// Time needed to execute the plan with the given feed override, including acceleration and
    /// deceleration. A feed override of zero means the plan never finishes, so it is rejected.
    /// </summary>
    public TimeSpan EstimateDuration(double feedOverride = 1.0)
    {
        if (!(feedOverride > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(feedOverride), feedOverride, "Feed override must be greater than zero.");
        }

        if (IsEmpty)
        {
            return TimeSpan.Zero;
        }

        var exitSpeeds = new double[Segments.Count];
        SpeedLookahead.ComputeExitSpeeds(Segments, feedOverride, exitSpeeds);

        var seconds = 0.0;
        var entry = 0.0;
        for (var i = 0; i < Segments.Count; i++)
        {
            var segment = Segments[i];
            var reachable = Math.Sqrt((entry * entry) + (2 * segment.Acceleration * segment.Length));
            var exit = Math.Min(exitSpeeds[i], reachable);
            var profile = TrapezoidProfile.Create(segment.Length, entry, exit, segment.SpeedCap(feedOverride), segment.Acceleration);
            seconds += profile.Duration;
            entry = exit;
        }

        return TimeSpan.FromSeconds(seconds);
    }
}
