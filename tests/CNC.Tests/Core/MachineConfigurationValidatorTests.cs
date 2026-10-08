using CNC.Core.Axes;
using CNC.Core.Configuration;

namespace CNC.Tests.Core;

public sealed class MachineConfigurationValidatorTests
{
    private static readonly MachineConfiguration Valid = MachineConfiguration.CreateDefault();

    [Fact]
    public void DefaultConfiguration_IsValid()
    {
        Assert.Empty(MachineConfigurationValidator.Validate(Valid));
    }

    [Fact]
    public void Default_HasAllLinearAxes()
    {
        Assert.All(LinearAxes.All, axis => Assert.Equal(axis, Valid.GetAxis(axis).Axis));
    }

    [Fact]
    public void BlankName_IsRejected()
    {
        AssertSingleError(Valid with { Name = " " }, "name");
    }

    [Fact]
    public void UnsupportedSchemaVersion_IsRejected()
    {
        AssertSingleError(Valid with { SchemaVersion = MachineConfiguration.CurrentSchemaVersion + 1 }, "schemaVersion");
    }

    [Fact]
    public void MissingAxis_IsRejected()
    {
        var config = Valid with { Axes = [.. Valid.Axes.Where(a => a.Axis != Axis.Y)] };

        AssertSingleError(config, "axes[Y]");
    }

    [Fact]
    public void DuplicateAxis_IsRejected()
    {
        var config = Valid with { Axes = [.. Valid.Axes, Valid.GetAxis(Axis.X)] };

        AssertSingleError(config, "axes[X]");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NonPositiveStepsPerUnit_IsRejected(double value)
    {
        AssertSingleError(WithX(a => a with { StepsPerUnit = value }), "axes[X].stepsPerUnit");
    }

    [Fact]
    public void NonPositiveVelocity_IsRejected()
    {
        var errors = MachineConfigurationValidator.Validate(WithX(a => a with { MaxVelocity = 0 }));

        Assert.Contains(errors, e => e.Path == "axes[X].maxVelocity");
    }

    [Fact]
    public void NonPositiveAcceleration_IsRejected()
    {
        AssertSingleError(WithX(a => a with { MaxAcceleration = -5 }), "axes[X].maxAcceleration");
    }

    [Fact]
    public void InvertedTravelLimits_AreRejected()
    {
        AssertSingleError(WithX(a => a with { MinPosition = 100, MaxPosition = 50, HomePosition = 75 }), "axes[X].maxPosition");
    }

    [Fact]
    public void HomePositionOutsideTravel_IsRejected()
    {
        AssertSingleError(WithX(a => a with { HomePosition = a.MaxPosition + 1 }), "axes[X].homePosition");
    }

    [Fact]
    public void HomingFasterThanMaxVelocity_IsRejected()
    {
        AssertSingleError(WithX(a => a with { HomingVelocity = a.MaxVelocity + 1 }), "axes[X].homingVelocity");
    }

    [Fact]
    public void SpindleMaxBelowMin_IsRejected()
    {
        AssertSingleError(Valid with { Spindle = Valid.Spindle with { MinRpm = 5000, MaxRpm = 1000 } }, "spindle.maxRpm");
    }

    [Fact]
    public void NegativeSpindleMin_IsRejected()
    {
        AssertSingleError(Valid with { Spindle = Valid.Spindle with { MinRpm = -1 } }, "spindle.minRpm");
    }

    [Fact]
    public void NonPositiveSpindleAcceleration_IsRejected()
    {
        AssertSingleError(Valid with { Spindle = Valid.Spindle with { Acceleration = 0 } }, "spindle.acceleration");
    }

    [Fact]
    public void WithAxis_ReplacesAxisAndKeepsOrder()
    {
        var updated = Valid.WithAxis(Valid.GetAxis(Axis.X) with { MaxVelocity = 1234 });

        Assert.Equal(1234, updated.GetAxis(Axis.X).MaxVelocity);
        Assert.Equal([Axis.X, Axis.Y, Axis.Z], updated.Axes.Select(a => a.Axis));
        Assert.Equal(5000, Valid.GetAxis(Axis.X).MaxVelocity);
    }

    [Fact]
    public void GetAxis_ThrowsForMissingAxis()
    {
        var config = Valid with { Axes = [] };

        Assert.Throws<KeyNotFoundException>(() => config.GetAxis(Axis.Z));
    }

    private static MachineConfiguration WithX(Func<AxisConfiguration, AxisConfiguration> change) =>
        Valid.WithAxis(change(Valid.GetAxis(Axis.X)));

    private static void AssertSingleError(MachineConfiguration configuration, string expectedPath)
    {
        var error = Assert.Single(MachineConfigurationValidator.Validate(configuration));
        Assert.Equal(expectedPath, error.Path);
    }
}
