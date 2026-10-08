using CNC.Core.Axes;
using CNC.Core.Geometry;

namespace CNC.GCode.Commands;

public abstract record MotionCommand(int LineNumber, Position Start, Position Target) : GCodeCommand(LineNumber);

/// <summary>G0: move at maximum machine velocity. The path between start and target is a straight line.</summary>
public sealed record RapidMoveCommand(int LineNumber, Position Start, Position Target)
    : MotionCommand(LineNumber, Start, Target);

/// <summary>G1: coordinated straight move at <paramref name="FeedRate"/> mm/min.</summary>
public sealed record LinearMoveCommand(int LineNumber, Position Start, Position Target, double FeedRate)
    : MotionCommand(LineNumber, Start, Target);

/// <summary>
/// G2/G3: circular (or helical, if the normal axis also moves) move in <paramref name="Plane"/>.
/// <paramref name="Center"/> holds the arc centre for the two plane axes; its normal-axis value equals the start.
/// </summary>
public sealed record ArcMoveCommand(
    int LineNumber,
    Position Start,
    Position Target,
    Position Center,
    ArcDirection Direction,
    Plane Plane,
    double FeedRate)
    : MotionCommand(LineNumber, Start, Target)
{
    public double Radius
    {
        get
        {
            var (first, second, _) = Plane.GetAxes();
            var d1 = Start[first] - Center[first];
            var d2 = Start[second] - Center[second];
            return Math.Sqrt((d1 * d1) + (d2 * d2));
        }
    }
}

/// <summary>
/// G28: rapid to the optional intermediate point, then return <paramref name="Axes"/> to the home
/// position. <see cref="MotionCommand.Target"/> is the home position expressed in work coordinates.
/// </summary>
public sealed record ReturnHomeCommand(
    int LineNumber,
    Position Start,
    Position? Intermediate,
    IReadOnlyList<Axis> Axes,
    Position Target)
    : MotionCommand(LineNumber, Start, Target);
