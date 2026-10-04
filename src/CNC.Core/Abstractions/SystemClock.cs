using System.Diagnostics;

namespace CNC.Core.Abstractions;

public sealed class SystemClock : IClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public TimeSpan Elapsed => _stopwatch.Elapsed;
}
