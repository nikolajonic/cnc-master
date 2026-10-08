using CNC.Core.Axes;

namespace CNC.Core.Geometry;

/// <summary>Active plane for circular interpolation (G17 / G18 / G19).</summary>
public enum Plane
{
    XY,
    XZ,
    YZ,
}

public enum ArcDirection
{
    /// <summary>G2, as seen looking down the plane normal from its positive side.</summary>
    Clockwise,

    /// <summary>G3.</summary>
    CounterClockwise,
}

public static class PlaneExtensions
{
    /// <summary>The two axes spanning the plane and the axis normal to it (the helical axis).</summary>
    public static (Axis First, Axis Second, Axis Normal) GetAxes(this Plane plane) => plane switch
    {
        Plane.XY => (Axis.X, Axis.Y, Axis.Z),
        Plane.XZ => (Axis.Z, Axis.X, Axis.Y),
        Plane.YZ => (Axis.Y, Axis.Z, Axis.X),
        _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, null),
    };
}
