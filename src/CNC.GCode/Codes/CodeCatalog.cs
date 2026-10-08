using System.Globalization;

namespace CNC.GCode.Codes;

public static class CodeCatalog
{
    public static ModalGroup GetGroup(GFunction code) => code switch
    {
        GFunction.G28 => ModalGroup.NonModal,
        GFunction.G0 or GFunction.G1 or GFunction.G2 or GFunction.G3 or GFunction.G80 => ModalGroup.Motion,
        GFunction.G17 or GFunction.G18 or GFunction.G19 => ModalGroup.Plane,
        GFunction.G20 or GFunction.G21 => ModalGroup.Units,
        GFunction.G40 or GFunction.G41 or GFunction.G42 => ModalGroup.CutterCompensation,
        GFunction.G43 or GFunction.G49 => ModalGroup.ToolLengthOffset,
        GFunction.G54 or GFunction.G55 or GFunction.G56 or GFunction.G57 or GFunction.G58 or GFunction.G59 => ModalGroup.CoordinateSystem,
        GFunction.G90 or GFunction.G91 => ModalGroup.DistanceMode,
        GFunction.G94 => ModalGroup.FeedRateMode,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    public static ModalGroup GetGroup(MFunction code) => code switch
    {
        MFunction.M0 or MFunction.M1 or MFunction.M2 or MFunction.M30 => ModalGroup.Stopping,
        MFunction.M3 or MFunction.M4 or MFunction.M5 => ModalGroup.Spindle,
        MFunction.M6 => ModalGroup.ToolChange,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    /// <summary>Maps a G word value (for example 1 or 43.1) to a supported code.</summary>
    public static bool TryGetGFunction(double value, out GFunction code)
    {
        var scaled = value * 10;
        var rounded = Math.Round(scaled);
        if (value >= 0 && Math.Abs(scaled - rounded) < 1e-6 && rounded <= int.MaxValue && Enum.IsDefined((GFunction)(int)rounded))
        {
            code = (GFunction)(int)rounded;
            return true;
        }

        code = default;
        return false;
    }

    public static bool TryGetMFunction(double value, out MFunction code)
    {
        if (value >= 0 && value == Math.Floor(value) && value <= int.MaxValue && Enum.IsDefined((MFunction)(int)value))
        {
            code = (MFunction)(int)value;
            return true;
        }

        code = default;
        return false;
    }

    public static string Format(GFunction code)
    {
        var value = (int)code;
        return value % 10 == 0
            ? string.Create(CultureInfo.InvariantCulture, $"G{value / 10}")
            : string.Create(CultureInfo.InvariantCulture, $"G{value / 10}.{value % 10}");
    }
}
