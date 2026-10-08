using CNC.Motion.Profiles;

namespace CNC.Tests.Motion;

public sealed class TrapezoidProfileTests
{
    [Fact]
    public void LongMove_AcceleratesCruisesAndDecelerates()
    {
        var profile = TrapezoidProfile.Create(length: 100, entrySpeed: 0, exitSpeed: 0, maxSpeed: 10, acceleration: 5);

        Assert.Equal(10, profile.PeakSpeed, 9);
        Assert.Equal(2, profile.AccelerationTime, 9);
        Assert.Equal(8, profile.CruiseTime, 9);
        Assert.Equal(2, profile.DecelerationTime, 9);
        Assert.Equal(12, profile.Duration, 9);
        Assert.Equal(5, profile.VelocityAt(1), 9);
        Assert.Equal(10, profile.VelocityAt(5), 9);
        Assert.Equal(5, profile.VelocityAt(11), 9);
        Assert.Equal(100, profile.DistanceAt(profile.Duration), 9);
    }

    [Fact]
    public void ShortMove_IsTriangular()
    {
        var profile = TrapezoidProfile.Create(length: 10, entrySpeed: 0, exitSpeed: 0, maxSpeed: 100, acceleration: 5);

        Assert.Equal(Math.Sqrt(50), profile.PeakSpeed, 9);
        Assert.Equal(0, profile.CruiseTime, 9);
        Assert.Equal(2 * Math.Sqrt(50) / 5, profile.Duration, 9);
        Assert.Equal(5, profile.DistanceAt(profile.Duration / 2), 9);
    }

    [Fact]
    public void NonZeroEntryAndExitSpeeds_AreHonoured()
    {
        var profile = TrapezoidProfile.Create(length: 100, entrySpeed: 5, exitSpeed: 2, maxSpeed: 10, acceleration: 5);

        Assert.Equal(1, profile.AccelerationTime, 9);
        Assert.Equal(1.6, profile.DecelerationTime, 9);
        Assert.Equal(8.29, profile.CruiseTime, 9);
        Assert.Equal(5, profile.VelocityAt(0), 9);
        Assert.Equal(2, profile.VelocityAt(profile.Duration), 9);
    }

    [Fact]
    public void UnreachableExitSpeed_IsLowered()
    {
        var profile = TrapezoidProfile.Create(length: 1, entrySpeed: 0, exitSpeed: 10, maxSpeed: 20, acceleration: 2);

        Assert.Equal(2, profile.ExitSpeed, 9);
        Assert.Equal(1, profile.Duration, 9);
    }

    [Fact]
    public void EntrySpeedTooHighToStop_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            TrapezoidProfile.Create(length: 1, entrySpeed: 10, exitSpeed: 0, maxSpeed: 20, acceleration: 1));
    }

    [Fact]
    public void ZeroLength_HasZeroDuration()
    {
        var profile = TrapezoidProfile.Create(length: 0, entrySpeed: 0, exitSpeed: 0, maxSpeed: 10, acceleration: 1);

        Assert.Equal(0, profile.Duration);
    }

    [Theory]
    [InlineData(100, 0, 0, 10, 5)]
    [InlineData(3, 0, 0, 50, 20)]
    [InlineData(50, 4, 7, 8, 3)]
    public void SampledProfile_RespectsSpeedAndAccelerationLimits(double length, double entry, double exit, double maxSpeed, double acceleration)
    {
        var profile = TrapezoidProfile.Create(length, entry, exit, maxSpeed, acceleration);
        const double dt = 0.001;

        var previousVelocity = profile.VelocityAt(0);
        var previousDistance = 0.0;
        for (var t = dt; t <= profile.Duration + dt; t += dt)
        {
            var velocity = profile.VelocityAt(t);
            var distance = profile.DistanceAt(t);

            Assert.True(velocity <= maxSpeed + 1e-9, $"Speed {velocity} exceeds {maxSpeed} at t={t}.");
            Assert.True(Math.Abs(velocity - previousVelocity) <= (acceleration * dt) + 1e-9, $"Acceleration exceeded at t={t}.");
            Assert.True(distance >= previousDistance - 1e-12, $"Distance went backwards at t={t}.");

            previousVelocity = velocity;
            previousDistance = distance;
        }

        Assert.Equal(length, profile.DistanceAt(profile.Duration), 9);
    }
}
