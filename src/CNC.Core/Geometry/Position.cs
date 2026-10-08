using System.Globalization;
using CNC.Core.Axes;

namespace CNC.Core.Geometry;

/// <summary>
/// An X/Y/Z triple in machine units. Used both for absolute positions and for displacements
/// between positions.
/// </summary>
public readonly record struct Position(double X, double Y, double Z)
{
    public static Position Zero { get; } = new(0, 0, 0);

    public double this[Axis axis] => axis switch
    {
        Axis.X => X,
        Axis.Y => Y,
        Axis.Z => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported axis."),
    };

    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public Position With(Axis axis, double value) => axis switch
    {
        Axis.X => this with { X = value },
        Axis.Y => this with { Y = value },
        Axis.Z => this with { Z = value },
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported axis."),
    };

    public double DistanceTo(Position other) => (other - this).Length;

    public static Position operator +(Position a, Position b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Position operator -(Position a, Position b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Position operator -(Position a) => new(-a.X, -a.Y, -a.Z);

    public static Position operator *(Position a, double factor) => new(a.X * factor, a.Y * factor, a.Z * factor);

    public static Position operator *(double factor, Position a) => a * factor;

    public static Position operator /(Position a, double divisor) => new(a.X / divisor, a.Y / divisor, a.Z / divisor);

    public static Position Add(Position left, Position right) => left + right;

    public static Position Subtract(Position left, Position right) => left - right;

    public static Position Negate(Position value) => -value;

    public static Position Multiply(Position value, double factor) => value * factor;

    public static Position Divide(Position value, double divisor) => value / divisor;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"X{X:0.####} Y{Y:0.####} Z{Z:0.####}");
}
