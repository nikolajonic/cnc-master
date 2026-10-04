namespace CNC.Core.Abstractions;

/// <summary>
/// Time source abstraction. Motion, simulation and timing code must use this instead of
/// <see cref="DateTime.Now"/> or <see cref="System.Diagnostics.Stopwatch"/> directly so that
/// tests can drive time deterministically.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Monotonic timestamp suitable for measuring elapsed intervals.</summary>
    TimeSpan Elapsed { get; }
}
