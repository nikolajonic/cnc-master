using CNC.Core.Coordinates;
using CNC.Core.Geometry;
using CNC.Core.Units;

namespace CNC.GCode.Interpretation;

/// <summary>
/// Context the interpreter needs from the machine. Positions and offsets are machine coordinates
/// in millimetres. For a preview the defaults (everything at zero) are sufficient; for execution
/// the controller supplies the live values so commands match the real machine.
/// </summary>
public sealed record InterpreterOptions
{
    public Position InitialMachinePosition { get; init; } = Position.Zero;

    public MeasurementSystem InitialUnits { get; init; } = MeasurementSystem.Metric;

    /// <summary>Machine position reached by G28.</summary>
    public Position HomeMachinePosition { get; init; } = Position.Zero;

    /// <summary>Machine coordinates of each work coordinate system origin. Missing entries are zero.</summary>
    public IReadOnlyDictionary<WorkCoordinateSystem, Position> WorkOffsets { get; init; } =
        new Dictionary<WorkCoordinateSystem, Position>();

    /// <summary>When true, lines starting with '/' are skipped.</summary>
    public bool BlockDeleteEnabled { get; init; } = true;

    /// <summary>Largest accepted difference between arc start and end radius, in millimetres.</summary>
    public double ArcAbsoluteTolerance { get; init; } = 0.01;

    /// <summary>Largest accepted radius difference as a fraction of the radius, for large arcs.</summary>
    public double ArcRelativeTolerance { get; init; } = 0.001;

    public Position GetWorkOffset(WorkCoordinateSystem system) =>
        WorkOffsets.TryGetValue(system, out var offset) ? offset : Position.Zero;
}
