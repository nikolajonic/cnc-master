using CNC.Core.Axes;
using CNC.Core.Geometry;
using CNC.Core.Units;

namespace CNC.Tests.Core;

public sealed class PositionTests
{
    [Fact]
    public void Arithmetic_IsComponentWise()
    {
        var a = new Position(1, 2, 3);
        var b = new Position(4, 5, 6);

        Assert.Equal(new Position(5, 7, 9), a + b);
        Assert.Equal(new Position(3, 3, 3), b - a);
        Assert.Equal(new Position(2, 4, 6), a * 2);
        Assert.Equal(new Position(2, 4, 6), 2 * a);
        Assert.Equal(new Position(0.5, 1, 1.5), a / 2);
        Assert.Equal(new Position(-1, -2, -3), -a);
    }

    [Fact]
    public void Indexer_ReturnsAxisValue()
    {
        var p = new Position(1, 2, 3);

        Assert.Equal(1, p[Axis.X]);
        Assert.Equal(2, p[Axis.Y]);
        Assert.Equal(3, p[Axis.Z]);
    }

    [Fact]
    public void With_ReplacesOnlyOneAxis()
    {
        Assert.Equal(new Position(1, 9, 3), new Position(1, 2, 3).With(Axis.Y, 9));
    }

    [Fact]
    public void DistanceTo_IsEuclidean()
    {
        Assert.Equal(5, new Position(0, 0, 0).DistanceTo(new Position(3, 4, 0)), 12);
        Assert.Equal(Math.Sqrt(3), Position.Zero.DistanceTo(new Position(1, 1, 1)), 12);
    }

    [Fact]
    public void ToString_UsesInvariantCulture()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("X1.5 Y-2 Z0.125", new Position(1.5, -2, 0.125).ToString());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void AxisStatus_Initial_IsUnhomedWithNoLimits()
    {
        var status = AxisStatus.Initial(Axis.Z);

        Assert.Equal(Axis.Z, status.Axis);
        Assert.False(status.IsHomed);
        Assert.False(status.AnyLimitTriggered);
        Assert.True(status.IsEnabled);
    }

    [Theory]
    [InlineData(1, MeasurementSystem.Imperial, MeasurementSystem.Metric, 25.4)]
    [InlineData(25.4, MeasurementSystem.Metric, MeasurementSystem.Imperial, 1)]
    [InlineData(7, MeasurementSystem.Metric, MeasurementSystem.Metric, 7)]
    public void ConvertLength_ConvertsBetweenSystems(double value, MeasurementSystem from, MeasurementSystem to, double expected)
    {
        Assert.Equal(expected, MeasurementSystemExtensions.ConvertLength(value, from, to), 12);
    }

    [Fact]
    public void Abbreviations_MatchSystem()
    {
        Assert.Equal("mm/min", MeasurementSystem.Metric.FeedRateAbbreviation());
        Assert.Equal("in", MeasurementSystem.Imperial.LengthAbbreviation());
    }
}
