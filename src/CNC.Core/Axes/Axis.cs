namespace CNC.Core.Axes;

/// <summary>Linear machine axes. Values are stable indices; rotary axes can be appended later.</summary>
public enum Axis
{
    X = 0,
    Y = 1,
    Z = 2,
}

public static class LinearAxes
{
    public static IReadOnlyList<Axis> All { get; } = [Axis.X, Axis.Y, Axis.Z];
}
