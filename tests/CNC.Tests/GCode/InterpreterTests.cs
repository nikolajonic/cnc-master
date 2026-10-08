using CNC.Core.Axes;
using CNC.Core.Coordinates;
using CNC.Core.Geometry;
using CNC.Core.Machine;
using CNC.Core.Units;
using CNC.GCode;
using CNC.GCode.Commands;
using CNC.GCode.Interpretation;
using CNC.GCode.Modal;

namespace CNC.Tests.GCode;

public sealed class InterpreterTests
{
    [Fact]
    public void InitialState_MatchesStartupDefaults()
    {
        var state = new GCodeInterpreter().State;

        Assert.Equal(MotionMode.None, state.Motion);
        Assert.Equal(Plane.XY, state.Plane);
        Assert.Equal(MeasurementSystem.Metric, state.Units);
        Assert.Equal(DistanceMode.Absolute, state.DistanceMode);
        Assert.Equal(WorkCoordinateSystem.G54, state.WorkCoordinateSystem);
        Assert.Null(state.FeedRate);
        Assert.Null(state.Spindle);
        Assert.Equal(Position.Zero, state.Position);
    }

    [Fact]
    public void MotionMode_IsModalAcrossBlocks()
    {
        var program = Run("G1 X10 F100", "Y20", "Z-1");

        Assert.False(program.HasErrors);
        Assert.All(program.Commands, c => Assert.IsType<LinearMoveCommand>(c));
        Assert.Equal(new Position(10, 20, -1), program.FinalState.Position);
    }

    [Fact]
    public void Moves_CarryStartTargetAndLineNumber()
    {
        var program = Run("G0 X5 Y5", "G1 X15 F300");

        var rapid = Assert.IsType<RapidMoveCommand>(program.Commands[0]);
        var linear = Assert.IsType<LinearMoveCommand>(program.Commands[1]);
        Assert.Equal((1, Position.Zero, new Position(5, 5, 0)), (rapid.LineNumber, rapid.Start, rapid.Target));
        Assert.Equal((2, new Position(5, 5, 0), new Position(15, 5, 0), 300d), (linear.LineNumber, linear.Start, linear.Target, linear.FeedRate));
    }

    [Fact]
    public void AxisWordsWithoutMotionMode_AreRejected()
    {
        var program = Run("X10");

        var error = Assert.Single(program.Errors);
        Assert.Equal(1, error.LineNumber);
        Assert.Contains("motion mode", error.Message, StringComparison.Ordinal);
        Assert.Empty(program.Commands);
    }

    [Fact]
    public void G80_CancelsMotionMode()
    {
        var program = Run("G1 X1 F100", "G80", "X5");

        Assert.Single(program.Commands);
        Assert.Equal(3, Assert.Single(program.Errors).LineNumber);
    }

    [Fact]
    public void G80WithAxisWords_IsRejected()
    {
        Assert.Contains("G80", Assert.Single(Run("G80 X1").Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Incremental_AddsToCurrentPosition()
    {
        var program = Run("G0 X10 Y10", "G91", "G0 X5", "X5 Y-2", "G90", "X0");

        Assert.Equal(
            [new Position(10, 10, 0), new Position(15, 10, 0), new Position(20, 8, 0), new Position(0, 8, 0)],
            program.Commands.Cast<MotionCommand>().Select(c => c.Target));
    }

    [Fact]
    public void ImperialUnits_AreConvertedToMillimetres()
    {
        var program = Run("G20", "G1 X1 Y0.5 F10");

        var move = Assert.IsType<LinearMoveCommand>(Assert.Single(program.Commands));
        Assert.Equal(25.4, move.Target.X, 9);
        Assert.Equal(12.7, move.Target.Y, 9);
        Assert.Equal(254, move.FeedRate, 9);
        Assert.Equal(MeasurementSystem.Imperial, program.FinalState.Units);
    }

    [Fact]
    public void UnitsChangeInSameBlock_AppliesToThatBlock()
    {
        var move = Assert.IsType<RapidMoveCommand>(Assert.Single(Run("G20 G0 X1").Commands));

        Assert.Equal(25.4, move.Target.X, 9);
    }

    [Fact]
    public void FeedMove_WithoutFeedRate_IsRejected()
    {
        var error = Assert.Single(Run("G1 X10").Errors);

        Assert.Contains("No feed rate", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroFeedRate_IsRejected()
    {
        Assert.Contains("greater than zero", Assert.Single(Run("G1 X10 F0").Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FeedRate_IsModal()
    {
        var program = Run("G1 X1 F250", "X2", "F400", "X3");

        Assert.Equal([250d, 250d, 400d], program.Commands.Cast<LinearMoveCommand>().Select(c => c.FeedRate));
    }

    [Fact]
    public void RapidMove_DoesNotNeedFeedRate()
    {
        Assert.False(Run("G0 X10").HasErrors);
    }

    [Fact]
    public void ErrorBlock_DoesNotChangeState()
    {
        var program = Run("G0 X5", "G1 X50", "G0 Y5");

        Assert.Equal(new Position(5, 5, 0), program.FinalState.Position);
        Assert.Equal(MotionMode.Rapid, program.FinalState.Motion);
        Assert.Equal(2, program.Commands.Count);
    }

    [Fact]
    public void SpindleCommands_UseProgrammedSpeedAndDirection()
    {
        var program = Run("S12000 M3", "S8000", "M4", "M5");

        Assert.Collection(
            program.Commands,
            c => Assert.Equal(new StartSpindleCommand(1, SpindleDirection.Clockwise, 12000), c),
            c => Assert.Equal(new SetSpindleSpeedCommand(2, 8000), c),
            c => Assert.Equal(new StartSpindleCommand(3, SpindleDirection.CounterClockwise, 8000), c),
            c => Assert.Equal(new StopSpindleCommand(4), c));
        Assert.Null(program.FinalState.Spindle);
    }

    [Fact]
    public void SpeedChangeWhileStopped_OnlyUpdatesState()
    {
        var program = Run("S5000");

        Assert.Empty(program.Commands);
        Assert.Equal(5000, program.FinalState.SpindleSpeed);
    }

    [Fact]
    public void SpindleStartWithoutSpeed_Warns()
    {
        var program = Run("M3");

        Assert.False(program.HasErrors);
        Assert.Contains(program.Warnings, w => w.Message.Contains("speed of 0", StringComparison.Ordinal));
    }

    [Fact]
    public void ToolChange_UsesSelectedTool()
    {
        var program = Run("T3", "M6");

        Assert.Equal(new ToolChangeCommand(2, 3), Assert.Single(program.Commands));
        Assert.Equal(3, program.FinalState.CurrentTool);
    }

    [Fact]
    public void ToolChange_WithoutTool_IsRejected()
    {
        Assert.Contains("T word", Assert.Single(Run("M6").Errors).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("M0", ProgramStopKind.Pause)]
    [InlineData("M1", ProgramStopKind.OptionalPause)]
    [InlineData("M2", ProgramStopKind.End)]
    [InlineData("M30", ProgramStopKind.EndAndRewind)]
    public void StopCodes_ProduceProgramStop(string code, ProgramStopKind kind)
    {
        var program = Run(code);

        Assert.Equal(new ProgramStopCommand(1, kind), Assert.Single(program.Commands));
    }

    [Fact]
    public void ProgramEnd_ResetsModalStateAndIgnoresFollowingLines()
    {
        var program = Run("G91 G18 S1000 M3", "M30", "G0 X100", "G0 Y100");

        Assert.True(program.FinalState.ProgramEnded);
        Assert.Equal(DistanceMode.Absolute, program.FinalState.DistanceMode);
        Assert.Equal(Plane.XY, program.FinalState.Plane);
        Assert.Null(program.FinalState.Spindle);
        Assert.DoesNotContain(program.Commands, c => c is MotionCommand);
        Assert.Single(program.Warnings, w => w.Message.Contains("ignored", StringComparison.Ordinal));
    }

    [Fact]
    public void StopRunsAfterMotionInSameBlock()
    {
        var program = Run("G0 X1 M0");

        Assert.IsType<RapidMoveCommand>(program.Commands[0]);
        Assert.IsType<ProgramStopCommand>(program.Commands[1]);
    }

    [Fact]
    public void WorkCoordinateSystem_ReexpressesPositionUsingOffsets()
    {
        var options = new InterpreterOptions
        {
            WorkOffsets = new Dictionary<WorkCoordinateSystem, Position>
            {
                [WorkCoordinateSystem.G54] = new(100, 50, 0),
                [WorkCoordinateSystem.G55] = new(200, 50, -10),
            },
            InitialMachinePosition = new Position(100, 50, 0),
        };

        var program = GCodeProgram.Parse("G55\nG0 X0", options);

        Assert.Equal(new SelectWorkCoordinateSystemCommand(1, WorkCoordinateSystem.G55), program.Commands[0]);
        var move = Assert.IsType<RapidMoveCommand>(program.Commands[1]);
        Assert.Equal(new Position(-100, 0, 10), move.Start);
        Assert.Equal(new Position(0, 0, 10), move.Target);
        Assert.Equal(WorkCoordinateSystem.G55, program.FinalState.WorkCoordinateSystem);
    }

    [Fact]
    public void InitialPosition_IsExpressedInG54()
    {
        var options = new InterpreterOptions
        {
            InitialMachinePosition = new Position(10, 10, 10),
            WorkOffsets = new Dictionary<WorkCoordinateSystem, Position> { [WorkCoordinateSystem.G54] = new(4, 3, 2) },
        };

        Assert.Equal(new Position(6, 7, 8), new GCodeInterpreter(options).State.Position);
    }

    [Fact]
    public void G28_WithoutAxes_ReturnsAllAxesHome()
    {
        var options = new InterpreterOptions { HomeMachinePosition = new Position(0, 0, 0) };
        var program = GCodeProgram.Parse("G0 X10 Y20 Z5\nG28", options);

        var home = Assert.IsType<ReturnHomeCommand>(program.Commands[1]);
        Assert.Null(home.Intermediate);
        Assert.Equal(LinearAxes.All, home.Axes);
        Assert.Equal(Position.Zero, home.Target);
        Assert.Equal(Position.Zero, program.FinalState.Position);
    }

    [Fact]
    public void G28_WithAxes_MovesThroughIntermediateAndHomesOnlyThoseAxes()
    {
        var program = Run("G0 X10 Y20 Z5", "G28 Z10");

        var home = Assert.IsType<ReturnHomeCommand>(program.Commands[1]);
        Assert.Equal(new Position(10, 20, 10), home.Intermediate);
        Assert.Equal([Axis.Z], home.Axes);
        Assert.Equal(new Position(10, 20, 0), home.Target);
    }

    [Fact]
    public void G28_DoesNotChangeMotionMode()
    {
        var program = Run("G1 X1 F100", "G28", "X5");

        Assert.IsType<LinearMoveCommand>(program.Commands[^1]);
    }

    [Fact]
    public void G28WithMotionCode_IsRejected()
    {
        Assert.Contains("G28", Assert.Single(Run("G28 G1 X1 F100").Errors).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("G41 D1", "not supported")]
    [InlineData("G42", "not supported")]
    [InlineData("G0 X1 D2", "D words")]
    public void CutterCompensation_IsRejected(string line, string expectedMessage)
    {
        Assert.Contains(expectedMessage, Assert.Single(Run(line).Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void G40_IsAccepted()
    {
        Assert.False(Run("G40").HasErrors);
    }

    [Fact]
    public void G43_RecordsOffsetAndWarnsThatItIsNotApplied()
    {
        var program = Run("G43 H2", "G49");

        Assert.Equal([new ToolLengthOffsetCommand(1, 2), new ToolLengthOffsetCommand(2, null)], program.Commands);
        Assert.Contains(program.Warnings, w => w.LineNumber == 1 && w.Message.Contains("tool table", StringComparison.Ordinal));
        Assert.Null(program.FinalState.ToolLengthOffset);
    }

    [Fact]
    public void G43_WithoutHOrTool_IsRejected()
    {
        Assert.Single(Run("G43").Errors);
    }

    [Fact]
    public void HWithoutG43_IsRejected()
    {
        Assert.Contains("G43", Assert.Single(Run("G0 X1 H1").Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ArcOffsetsWithoutArcMode_AreRejected()
    {
        Assert.Contains("G2 or G3", Assert.Single(Run("G1 X1 I5 F100").Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockDelete_SkipsLineByDefault()
    {
        Assert.Single(Run("G0 X1", "/G0 X2").Commands);
    }

    [Fact]
    public void BlockDelete_CanBeDisabled()
    {
        var program = GCodeProgram.Parse("G0 X1\n/G0 X2", new InterpreterOptions { BlockDeleteEnabled = false });

        Assert.Equal(2, program.Commands.Count);
    }

    [Fact]
    public void Interpreter_CanBeDrivenOneBlockAtATime()
    {
        var interpreter = new GCodeInterpreter();

        var first = interpreter.Execute(CNC.GCode.Parsing.GCodeBlockParser.Parse("G0 X3", 1));
        var second = interpreter.Execute(CNC.GCode.Parsing.GCodeBlockParser.Parse("Y4", 2));

        Assert.Single(first.Commands);
        Assert.Single(second.Commands);
        Assert.Equal(new Position(3, 4, 0), interpreter.State.Position);
    }

    private static GCodeProgram Run(params string[] lines) => GCodeProgram.Parse(string.Join('\n', lines));
}
