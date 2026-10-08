namespace CNC.Motion.Planning;

public sealed record PlannerOptions
{
    /// <summary>Maximum distance (machine units) between an arc and the chords that approximate it.</summary>
    public double ArcTolerance { get; init; } = 0.002;

    /// <summary>
    /// Allowed deviation (machine units) from the programmed corner when blending two segments.
    /// Larger values give faster cornering; zero forces an exact stop at every corner.
    /// </summary>
    public double JunctionDeviation { get; init; } = 0.01;

    /// <summary>Moves shorter than this are dropped; they carry no meaningful motion.</summary>
    public double MinimumSegmentLength { get; init; } = 1e-6;

    /// <summary>Upper bound on chords per arc, protecting against huge arcs with tiny tolerances.</summary>
    public int MaxSegmentsPerArc { get; init; } = 10_000;
}
