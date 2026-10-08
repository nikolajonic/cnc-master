using CNC.Core.Abstractions;

namespace CNC.Tests.TestSupport;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public TimeSpan Elapsed { get; set; }
}
