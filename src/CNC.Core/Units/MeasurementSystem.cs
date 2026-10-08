namespace CNC.Core.Units;

public enum MeasurementSystem
{
    /// <summary>Millimetres.</summary>
    Metric,

    /// <summary>Inches.</summary>
    Imperial,
}

public static class MeasurementSystemExtensions
{
    public const double MillimetersPerInch = 25.4;

    public static string LengthAbbreviation(this MeasurementSystem system) => system switch
    {
        MeasurementSystem.Metric => "mm",
        MeasurementSystem.Imperial => "in",
        _ => throw new ArgumentOutOfRangeException(nameof(system), system, null),
    };

    public static string FeedRateAbbreviation(this MeasurementSystem system) => system.LengthAbbreviation() + "/min";

    /// <summary>Converts a length expressed in <paramref name="from"/> units into <paramref name="to"/> units.</summary>
    public static double ConvertLength(double value, MeasurementSystem from, MeasurementSystem to) => (from, to) switch
    {
        _ when from == to => value,
        (MeasurementSystem.Imperial, MeasurementSystem.Metric) => value * MillimetersPerInch,
        (MeasurementSystem.Metric, MeasurementSystem.Imperial) => value / MillimetersPerInch,
        _ => throw new ArgumentOutOfRangeException(nameof(to), to, null),
    };
}
