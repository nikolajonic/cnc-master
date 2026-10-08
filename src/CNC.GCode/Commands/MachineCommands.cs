using CNC.Core.Coordinates;
using CNC.Core.Machine;

namespace CNC.GCode.Commands;

/// <summary>M3/M4: start the spindle at <paramref name="Rpm"/>.</summary>
public sealed record StartSpindleCommand(int LineNumber, SpindleDirection Direction, double Rpm) : GCodeCommand(LineNumber);

/// <summary>M5.</summary>
public sealed record StopSpindleCommand(int LineNumber) : GCodeCommand(LineNumber);

/// <summary>An S word while the spindle is already running.</summary>
public sealed record SetSpindleSpeedCommand(int LineNumber, double Rpm) : GCodeCommand(LineNumber);

/// <summary>M6: change to the tool selected by the last T word.</summary>
public sealed record ToolChangeCommand(int LineNumber, int Tool) : GCodeCommand(LineNumber);

/// <summary>G54-G59.</summary>
public sealed record SelectWorkCoordinateSystemCommand(int LineNumber, WorkCoordinateSystem System) : GCodeCommand(LineNumber);

/// <summary>G43 (with a tool number) or G49 (<paramref name="Tool"/> is <c>null</c>).</summary>
public sealed record ToolLengthOffsetCommand(int LineNumber, int? Tool) : GCodeCommand(LineNumber);

public enum ProgramStopKind
{
    /// <summary>M0: always pause until cycle start.</summary>
    Pause,

    /// <summary>M1: pause only when optional stop is enabled.</summary>
    OptionalPause,

    /// <summary>M2: end of program.</summary>
    End,

    /// <summary>M30: end of program and rewind.</summary>
    EndAndRewind,
}

public sealed record ProgramStopCommand(int LineNumber, ProgramStopKind Kind) : GCodeCommand(LineNumber)
{
    public bool EndsProgram => Kind is ProgramStopKind.End or ProgramStopKind.EndAndRewind;
}
