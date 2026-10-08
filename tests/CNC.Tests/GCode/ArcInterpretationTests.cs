using CNC.Core.Geometry;
using CNC.GCode;
using CNC.GCode.Commands;

namespace CNC.Tests.GCode;

public sealed class ArcInterpretationTests
{
    [Fact]
    public void ClockwiseArc_InXYPlane_ComputesCentreFromIJ()
    {
        var arc = SingleArc("G0 X0 Y0", "G2 X10 Y0 I5 J0 F200");

        Assert.Equal(ArcDirection.Clockwise, arc.Direction);
        Assert.Equal(Plane.XY, arc.Plane);
        Assert.Equal(new Position(5, 0, 0), arc.Center);
        Assert.Equal(new Position(10, 0, 0), arc.Target);
        Assert.Equal(5, arc.Radius, 9);
        Assert.Equal(200, arc.FeedRate);
    }

    [Fact]
    public void CounterClockwiseArc_IsRecognised()
    {
        Assert.Equal(ArcDirection.CounterClockwise, SingleArc("G3 X10 Y10 I10 F100").Direction);
    }

    [Fact]
    public void ArcCentreOffsets_AreAlwaysIncrementalFromStart()
    {
        var arc = SingleArc("G0 X20 Y20", "G90 G2 X30 Y20 I5 F100");

        Assert.Equal(new Position(25, 20, 0), arc.Center);
    }

    [Fact]
    public void HelicalArc_MovesNormalAxis()
    {
        var arc = SingleArc("G2 X10 Y0 Z-2 I5 F100");

        Assert.Equal(-2, arc.Target.Z);
        Assert.Equal(0, arc.Center.Z);
    }

    [Fact]
    public void FullCircle_IsAllowedWhenEndEqualsStart()
    {
        var arc = SingleArc("G0 X10 Y0", "G2 X10 Y0 I-10 F100");

        Assert.Equal(arc.Start, arc.Target);
        Assert.Equal(10, arc.Radius, 9);
    }

    [Fact]
    public void XZPlaneArc_UsesIAndK()
    {
        var arc = SingleArc("G18", "G2 X10 Z0 I5 K0 F100");

        Assert.Equal(Plane.XZ, arc.Plane);
        Assert.Equal(new Position(5, 0, 0), arc.Center);
    }

    [Fact]
    public void YZPlaneArc_UsesJAndK()
    {
        var arc = SingleArc("G19", "G3 Y0 Z10 K5 F100");

        Assert.Equal(Plane.YZ, arc.Plane);
        Assert.Equal(new Position(0, 0, 5), arc.Center);
    }

    [Theory]
    [InlineData("G17", "G2 X10 I5 K1 F100", "K is not valid")]
    [InlineData("G18", "G2 X10 I5 J1 F100", "J is not valid")]
    [InlineData("G19", "G2 Y10 I1 J5 F100", "I is not valid")]
    public void OffsetForNormalAxis_IsRejected(string plane, string arc, string expected)
    {
        var program = GCodeProgram.Parse($"{plane}\n{arc}");

        Assert.Contains(expected, Assert.Single(program.Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ArcWithoutCentreOffset_IsRejected()
    {
        var program = GCodeProgram.Parse("G2 X10 F100");

        Assert.Contains("centre offset", Assert.Single(program.Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ArcEndNotOnCircle_IsRejectedWithRadii()
    {
        var program = GCodeProgram.Parse("G2 X12 Y0 I5 F100");

        var error = Assert.Single(program.Errors);
        Assert.Contains("not on the arc", error.Message, StringComparison.Ordinal);
        Assert.Contains("5 mm", error.Message, StringComparison.Ordinal);
        Assert.Contains("7 mm", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SmallRadiusMismatch_WithinToleranceIsAccepted()
    {
        Assert.False(GCodeProgram.Parse("G2 X10.005 Y0 I5 F100").HasErrors);
    }

    [Fact]
    public void ZeroRadiusArc_IsRejected()
    {
        Assert.Contains("radius is zero", Assert.Single(GCodeProgram.Parse("G2 X0 Y0 I0 J0 F100").Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ArcOffsetsWithoutEndPoint_AreRejected()
    {
        Assert.Contains("no end point", Assert.Single(GCodeProgram.Parse("G2 I5 F100").Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ArcMode_IsModal()
    {
        var program = GCodeProgram.Parse("G2 X10 I5 F100\nX0 I-5");

        Assert.Equal(2, program.Commands.OfType<ArcMoveCommand>().Count());
    }

    [Fact]
    public void ImperialArc_ConvertsOffsets()
    {
        var arc = SingleArc("G20", "G2 X1 I0.5 F10");

        Assert.Equal(12.7, arc.Center.X, 9);
        Assert.Equal(12.7, arc.Radius, 9);
    }

    private static ArcMoveCommand SingleArc(params string[] lines)
    {
        var program = GCodeProgram.Parse(string.Join('\n', lines));
        Assert.False(program.HasErrors, string.Join(Environment.NewLine, program.Errors));
        return Assert.Single(program.Commands.OfType<ArcMoveCommand>());
    }
}
