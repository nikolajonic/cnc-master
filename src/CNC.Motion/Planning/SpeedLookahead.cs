namespace CNC.Motion.Planning;

/// <summary>
/// Backward pass over the planned segments: the highest speed at which each segment may be left
/// so that every later corner, feed limit and the final stop can still be honoured without
/// exceeding the acceleration limits.
/// </summary>
public static class SpeedLookahead
{
    /// <summary>
    /// Fills <paramref name="exitSpeeds"/>[<paramref name="fromIndex"/>..] with the maximum exit speed of each segment.
    /// </summary>
    public static void ComputeExitSpeeds(
        IReadOnlyList<MotionSegment> segments,
        double feedOverride,
        double[] exitSpeeds,
        int fromIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(exitSpeeds);
        if (exitSpeeds.Length < segments.Count)
        {
            throw new ArgumentException("The exit speed buffer is smaller than the segment list.", nameof(exitSpeeds));
        }

        var last = segments.Count - 1;
        if (last < 0)
        {
            return;
        }

        exitSpeeds[last] = 0;
        for (var i = last - 1; i >= Math.Max(fromIndex, 0); i--)
        {
            var current = segments[i];
            var next = segments[i + 1];
            var corner = Math.Min(current.JunctionSpeed, Math.Min(current.SpeedCap(feedOverride), next.SpeedCap(feedOverride)));
            var reachable = Math.Sqrt((exitSpeeds[i + 1] * exitSpeeds[i + 1]) + (2 * next.Acceleration * next.Length));
            exitSpeeds[i] = Math.Min(corner, reachable);
        }
    }
}
