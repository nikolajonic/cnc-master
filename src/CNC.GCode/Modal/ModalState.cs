using CNC.Core.Coordinates;
using CNC.Core.Geometry;
using CNC.Core.Machine;
using CNC.Core.Units;

namespace CNC.GCode.Modal;

public enum MotionMode
{
    /// <summary>No motion mode is active (startup or after G80). Axis words alone are rejected.</summary>
    None,
    Rapid,
    Linear,
    ArcClockwise,
    ArcCounterClockwise,
}

public enum DistanceMode
{
    Absolute,
    Incremental,
}

public enum FeedRateMode
{
    UnitsPerMinute,
}

public enum CutterCompensation
{
    Off,
}

/// <summary>
/// The interpreter's modal state between blocks. <see cref="Position"/> and <see cref="FeedRate"/>
/// are always stored in millimetres regardless of the active <see cref="Units"/>.
/// </summary>
public sealed record ModalState
{
    public MotionMode Motion { get; init; } = MotionMode.None;

    public Plane Plane { get; init; } = Plane.XY;

    public MeasurementSystem Units { get; init; } = MeasurementSystem.Metric;

    public DistanceMode DistanceMode { get; init; } = DistanceMode.Absolute;

    public FeedRateMode FeedRateMode { get; init; } = FeedRateMode.UnitsPerMinute;

    public CutterCompensation CutterCompensation { get; init; } = CutterCompensation.Off;

    /// <summary>Tool number whose length offset is active (G43), or <c>null</c> when cancelled (G49).</summary>
    public int? ToolLengthOffset { get; init; }

    public WorkCoordinateSystem WorkCoordinateSystem { get; init; } = WorkCoordinateSystem.G54;

    /// <summary>Millimetres per minute, or <c>null</c> until an F word has been programmed.</summary>
    public double? FeedRate { get; init; }

    public double SpindleSpeed { get; init; }

    /// <summary>Running direction, or <c>null</c> while the spindle is stopped.</summary>
    public SpindleDirection? Spindle { get; init; }

    /// <summary>Tool selected by the last T word, waiting for M6.</summary>
    public int? SelectedTool { get; init; }

    public int? CurrentTool { get; init; }

    /// <summary>Current position in the active work coordinate system, in millimetres.</summary>
    public Position Position { get; init; } = Position.Zero;

    public bool ProgramEnded { get; init; }

    /// <summary>The modal values RS274/NGC restores at program end (M2/M30). Position, units and tools are kept.</summary>
    public ModalState ResetForProgramEnd() => this with
    {
        Motion = MotionMode.Linear,
        Plane = Plane.XY,
        DistanceMode = DistanceMode.Absolute,
        FeedRateMode = FeedRateMode.UnitsPerMinute,
        CutterCompensation = CutterCompensation.Off,
        WorkCoordinateSystem = WorkCoordinateSystem.G54,
        Spindle = null,
        ProgramEnded = true,
    };
}
