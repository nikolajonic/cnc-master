using CNC.Core.Abstractions;

namespace CNC.Tests.Core;

public sealed class SystemClockTests
{
    [Fact]
    public void Elapsed_IsMonotonic()
    {
        var clock = new SystemClock();

        var first = clock.Elapsed;
        Thread.Sleep(5);
        var second = clock.Elapsed;

        Assert.True(second > first);
    }

    [Fact]
    public void UtcNow_IsUtc()
    {
        Assert.Equal(TimeSpan.Zero, new SystemClock().UtcNow.Offset);
    }
}
