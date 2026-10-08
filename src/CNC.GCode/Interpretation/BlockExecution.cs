using System.Globalization;
using CNC.Core.Axes;
using CNC.Core.Coordinates;
using CNC.Core.Geometry;
using CNC.Core.Machine;
using CNC.Core.Units;
using CNC.GCode.Codes;
using CNC.GCode.Commands;
using CNC.GCode.Diagnostics;
using CNC.GCode.Modal;
using CNC.GCode.Parsing;

namespace CNC.GCode.Interpretation;

/// <summary>
/// Interprets one block. Steps run in the RS274/NGC order of execution: units, feed, speed, tool
/// selection, tool change, spindle, plane, cutter compensation, tool length offset, coordinate
/// system, distance mode, motion (or G28), then program stops.
/// </summary>
internal sealed class BlockExecution
{
    private readonly GCodeBlock _block;
    private readonly InterpreterOptions _options;
    private readonly List<GCodeCommand> _commands = [];
    private readonly List<GCodeDiagnostic> _diagnostics = [];

    public BlockExecution(GCodeBlock block, ModalState state, InterpreterOptions options)
    {
        _block = block;
        State = state;
        _options = options;
    }

    public ModalState State { get; private set; }

    public IReadOnlyList<GCodeCommand> Commands => _commands;

    public IReadOnlyList<GCodeDiagnostic> Diagnostics => _diagnostics;

    public bool HasErrors { get; private set; }

    private int Line => _block.LineNumber;

    public void Run()
    {
        ApplyUnits();
        ApplyFeedRate();
        ApplyToolSelection();
        _ = ApplyToolChange()
            && ApplySpindle()
            && ApplyPlane()
            && ApplyCutterCompensation()
            && ApplyToolLengthOffset()
            && ApplyCoordinateSystem()
            && ApplyDistanceMode()
            && ApplyMotion()
            && ApplyStop();
    }

    private void ApplyUnits()
    {
        switch (_block.GetGFunction(ModalGroup.Units))
        {
            case GFunction.G20:
                State = State with { Units = MeasurementSystem.Imperial };
                break;
            case GFunction.G21:
                State = State with { Units = MeasurementSystem.Metric };
                break;
        }
    }

    private void ApplyFeedRate()
    {
        if (_block.F is { } feed)
        {
            State = State with { FeedRate = ToMillimeters(feed) };
        }
    }

    private void ApplyToolSelection()
    {
        if (_block.T is { } tool)
        {
            State = State with { SelectedTool = tool };
        }
    }

    private bool ApplyToolChange()
    {
        if (_block.GetMFunction(ModalGroup.ToolChange) is null)
        {
            return true;
        }

        if (State.SelectedTool is not { } tool)
        {
            return Error("M6", "M6 requires a tool selected with a T word.");
        }

        State = State with { CurrentTool = tool };
        _commands.Add(new ToolChangeCommand(Line, tool));
        return true;
    }

    private bool ApplySpindle()
    {
        if (_block.S is { } speed)
        {
            State = State with { SpindleSpeed = speed };
        }

        switch (_block.GetMFunction(ModalGroup.Spindle))
        {
            case MFunction.M3:
                StartSpindle(SpindleDirection.Clockwise, "M3");
                break;
            case MFunction.M4:
                StartSpindle(SpindleDirection.CounterClockwise, "M4");
                break;
            case MFunction.M5:
                State = State with { Spindle = null };
                _commands.Add(new StopSpindleCommand(Line));
                break;
            default:
                if (_block.S.HasValue && State.Spindle.HasValue)
                {
                    _commands.Add(new SetSpindleSpeedCommand(Line, State.SpindleSpeed));
                }

                break;
        }

        return true;
    }

    private void StartSpindle(SpindleDirection direction, string code)
    {
        if (State.SpindleSpeed <= 0)
        {
            Warning(code, "Spindle started with a speed of 0; program an S word.");
        }

        State = State with { Spindle = direction };
        _commands.Add(new StartSpindleCommand(Line, direction, State.SpindleSpeed));
    }

    private bool ApplyPlane()
    {
        switch (_block.GetGFunction(ModalGroup.Plane))
        {
            case GFunction.G17:
                State = State with { Plane = Plane.XY };
                break;
            case GFunction.G18:
                State = State with { Plane = Plane.XZ };
                break;
            case GFunction.G19:
                State = State with { Plane = Plane.YZ };
                break;
        }

        return true;
    }

    private bool ApplyCutterCompensation()
    {
        var code = _block.GetGFunction(ModalGroup.CutterCompensation);
        if (code is GFunction.G41 or GFunction.G42)
        {
            // Running a compensated program without compensation would cut the wrong contour.
            return Error(CodeCatalog.Format(code.Value),
                "Cutter radius compensation (G41/G42) is not supported. Post-process the program with compensation computed in CAM.");
        }

        if (_block.D.HasValue)
        {
            return Error("D", "D words are only used with cutter radius compensation (G41/G42), which is not supported.");
        }

        if (code == GFunction.G40)
        {
            State = State with { CutterCompensation = CutterCompensation.Off };
        }

        return true;
    }

    private bool ApplyToolLengthOffset()
    {
        var code = _block.GetGFunction(ModalGroup.ToolLengthOffset);
        if (_block.H.HasValue && code != GFunction.G43)
        {
            return Error("H", "H words are only valid together with G43.");
        }

        if (code == GFunction.G43)
        {
            if ((_block.H ?? State.CurrentTool) is not { } tool)
            {
                return Error("G43", "G43 requires an H word or a tool loaded with M6.");
            }

            Warning("G43", "Tool length offsets are not applied yet: no tool table is configured. Z moves use programmed values.");
            State = State with { ToolLengthOffset = tool };
            _commands.Add(new ToolLengthOffsetCommand(Line, tool));
        }
        else if (code == GFunction.G49)
        {
            State = State with { ToolLengthOffset = null };
            _commands.Add(new ToolLengthOffsetCommand(Line, null));
        }

        return true;
    }

    private bool ApplyCoordinateSystem()
    {
        if (_block.GetGFunction(ModalGroup.CoordinateSystem) is not { } code)
        {
            return true;
        }

        var system = code switch
        {
            GFunction.G54 => WorkCoordinateSystem.G54,
            GFunction.G55 => WorkCoordinateSystem.G55,
            GFunction.G56 => WorkCoordinateSystem.G56,
            GFunction.G57 => WorkCoordinateSystem.G57,
            GFunction.G58 => WorkCoordinateSystem.G58,
            _ => WorkCoordinateSystem.G59,
        };

        if (system != State.WorkCoordinateSystem)
        {
            // The tool does not move; its coordinates are re-expressed in the new system.
            var machinePosition = State.Position + _options.GetWorkOffset(State.WorkCoordinateSystem);
            State = State with
            {
                WorkCoordinateSystem = system,
                Position = machinePosition - _options.GetWorkOffset(system),
            };
        }

        _commands.Add(new SelectWorkCoordinateSystemCommand(Line, system));
        return true;
    }

    private bool ApplyDistanceMode()
    {
        switch (_block.GetGFunction(ModalGroup.DistanceMode))
        {
            case GFunction.G90:
                State = State with { DistanceMode = DistanceMode.Absolute };
                break;
            case GFunction.G91:
                State = State with { DistanceMode = DistanceMode.Incremental };
                break;
        }

        return true;
    }

    private bool ApplyMotion()
    {
        var motionCode = _block.GetGFunction(ModalGroup.Motion);
        var isHome = _block.Has(GFunction.G28);

        if (isHome && motionCode is not null and not GFunction.G80)
        {
            return Error("G28", $"G28 cannot be combined with {CodeCatalog.Format(motionCode.Value)}: both use the axis words.");
        }

        if (motionCode is { } code)
        {
            State = State with
            {
                Motion = code switch
                {
                    GFunction.G0 => MotionMode.Rapid,
                    GFunction.G1 => MotionMode.Linear,
                    GFunction.G2 => MotionMode.ArcClockwise,
                    GFunction.G3 => MotionMode.ArcCounterClockwise,
                    _ => MotionMode.None,
                },
            };
        }

        if (isHome)
        {
            return ReturnHome();
        }

        var isArc = State.Motion is MotionMode.ArcClockwise or MotionMode.ArcCounterClockwise;
        if (_block.HasArcOffsets && !isArc)
        {
            return Error(FirstArcOffsetText(), "I, J and K words are only valid with G2 or G3.");
        }

        if (!_block.HasAxisWords)
        {
            return !_block.HasArcOffsets || Error(FirstArcOffsetText(), "Arc has no end point; program X, Y or Z.");
        }

        var start = State.Position;
        var target = ResolveTarget();

        GCodeCommand command;
        switch (State.Motion)
        {
            case MotionMode.None:
                return Error(FirstAxisText(), motionCode == GFunction.G80
                    ? "Axis words are not allowed after G80 cancels the motion mode."
                    : "Axis words without an active motion mode. Program G0, G1, G2 or G3 first.");

            case MotionMode.Rapid:
                command = new RapidMoveCommand(Line, start, target);
                break;

            case MotionMode.Linear:
                if (!TryGetFeedRate(out var feed))
                {
                    return false;
                }

                command = new LinearMoveCommand(Line, start, target, feed);
                break;

            default:
                if (!TryGetFeedRate(out var arcFeed) || !TryBuildArc(start, target, arcFeed, out var arc))
                {
                    return false;
                }

                command = arc;
                break;
        }

        State = State with { Position = target };
        _commands.Add(command);
        return true;
    }

    private bool ReturnHome()
    {
        var start = State.Position;
        Position? intermediate = null;
        List<Axis> axes;

        if (_block.HasAxisWords)
        {
            intermediate = ResolveTarget();
            axes = [.. LinearAxes.All.Where(axis => AxisWord(axis).HasValue)];
        }
        else
        {
            axes = [.. LinearAxes.All];
        }

        var home = _options.HomeMachinePosition - _options.GetWorkOffset(State.WorkCoordinateSystem);
        var final = intermediate ?? start;
        foreach (var axis in axes)
        {
            final = final.With(axis, home[axis]);
        }

        State = State with { Position = final };
        _commands.Add(new ReturnHomeCommand(Line, start, intermediate, axes, final));
        return true;
    }

    private bool TryBuildArc(Position start, Position target, double feed, out ArcMoveCommand arc)
    {
        arc = null!;
        var code = State.Motion == MotionMode.ArcClockwise ? "G2" : "G3";
        var (first, second, normal) = State.Plane.GetAxes();

        if (OffsetWord(normal).HasValue)
        {
            return Error(OffsetLetter(normal).ToString(),
                $"{OffsetLetter(normal)} is not valid for an arc in the {State.Plane} plane ({PlaneCode()}).");
        }

        if (!OffsetWord(first).HasValue && !OffsetWord(second).HasValue)
        {
            return Error(code,
                $"Arc in the {State.Plane} plane needs a centre offset: program {OffsetLetter(first)} and/or {OffsetLetter(second)}.");
        }

        var center = start
            .With(first, start[first] + ToMillimeters(OffsetWord(first) ?? 0))
            .With(second, start[second] + ToMillimeters(OffsetWord(second) ?? 0));

        var startRadius = PlanarDistance(start, center, first, second);
        var endRadius = PlanarDistance(target, center, first, second);

        if (startRadius < 1e-6)
        {
            return Error(code, "Arc radius is zero: the centre offset places the centre at the start point.");
        }

        var tolerance = Math.Max(_options.ArcAbsoluteTolerance, _options.ArcRelativeTolerance * startRadius);
        if (Math.Abs(startRadius - endRadius) > tolerance)
        {
            return Error(code, string.Create(CultureInfo.InvariantCulture,
                $"Arc end point is not on the arc: radius at start is {startRadius:0.####} mm but {endRadius:0.####} mm at the end."));
        }

        var direction = State.Motion == MotionMode.ArcClockwise ? ArcDirection.Clockwise : ArcDirection.CounterClockwise;
        arc = new ArcMoveCommand(Line, start, target, center, direction, State.Plane, feed);
        return true;
    }

    private bool ApplyStop()
    {
        var kind = _block.GetMFunction(ModalGroup.Stopping) switch
        {
            MFunction.M0 => ProgramStopKind.Pause,
            MFunction.M1 => ProgramStopKind.OptionalPause,
            MFunction.M2 => ProgramStopKind.End,
            MFunction.M30 => ProgramStopKind.EndAndRewind,
            _ => (ProgramStopKind?)null,
        };

        if (kind is not { } stopKind)
        {
            return true;
        }

        var command = new ProgramStopCommand(Line, stopKind);
        if (command.EndsProgram)
        {
            State = State.ResetForProgramEnd();
        }

        _commands.Add(command);
        return true;
    }

    private bool TryGetFeedRate(out double feed)
    {
        feed = State.FeedRate ?? 0;
        if (State.FeedRate is null)
        {
            return Error(MotionCodeText(), "No feed rate programmed. Add an F word before G1, G2 or G3 moves.");
        }

        return feed > 0 || Error("F", "Feed rate must be greater than zero.");
    }

    private Position ResolveTarget()
    {
        var target = State.Position;
        foreach (var axis in LinearAxes.All)
        {
            if (AxisWord(axis) is { } value)
            {
                var mm = ToMillimeters(value);
                target = target.With(axis, State.DistanceMode == DistanceMode.Incremental ? target[axis] + mm : mm);
            }
        }

        return target;
    }

    private double ToMillimeters(double value) =>
        MeasurementSystemExtensions.ConvertLength(value, State.Units, MeasurementSystem.Metric);

    private static double PlanarDistance(Position a, Position b, Axis first, Axis second)
    {
        var d1 = a[first] - b[first];
        var d2 = a[second] - b[second];
        return Math.Sqrt((d1 * d1) + (d2 * d2));
    }

    private double? AxisWord(Axis axis) => axis switch
    {
        Axis.X => _block.X,
        Axis.Y => _block.Y,
        _ => _block.Z,
    };

    private double? OffsetWord(Axis axis) => axis switch
    {
        Axis.X => _block.I,
        Axis.Y => _block.J,
        _ => _block.K,
    };

    private static char OffsetLetter(Axis axis) => axis switch
    {
        Axis.X => 'I',
        Axis.Y => 'J',
        _ => 'K',
    };

    private string PlaneCode() => State.Plane switch
    {
        Plane.XY => "G17",
        Plane.XZ => "G18",
        _ => "G19",
    };

    private string MotionCodeText() => State.Motion switch
    {
        MotionMode.Linear => "G1",
        MotionMode.ArcClockwise => "G2",
        MotionMode.ArcCounterClockwise => "G3",
        _ => "G0",
    };

    private string FirstAxisText() => _block.X.HasValue ? "X" : _block.Y.HasValue ? "Y" : "Z";

    private string FirstArcOffsetText() => _block.I.HasValue ? "I" : _block.J.HasValue ? "J" : "K";

    private bool Error(string offendingText, string message)
    {
        HasErrors = true;
        _diagnostics.Add(GCodeDiagnostic.Error(Line, 0, offendingText, message));
        return false;
    }

    private void Warning(string offendingText, string message)
    {
        _diagnostics.Add(GCodeDiagnostic.Warning(Line, 0, offendingText, message));
    }
}
