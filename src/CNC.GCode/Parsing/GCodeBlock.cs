using CNC.GCode.Codes;
using CNC.GCode.Diagnostics;

namespace CNC.GCode.Parsing;

/// <summary>
/// One line of G-code after syntax checking: the codes it contains and the values of its words.
/// Word values are exactly as written; units and distance mode are applied by the interpreter.
/// </summary>
public sealed class GCodeBlock
{
    public required int LineNumber { get; init; }

    public required string Text { get; init; }

    public string? Comment { get; init; }

    public bool IsBlockDelete { get; init; }

    public bool IsProgramDelimiter { get; init; }

    public IReadOnlyList<GFunction> GFunctions { get; init; } = [];

    public IReadOnlyList<MFunction> MFunctions { get; init; } = [];

    public double? X { get; init; }

    public double? Y { get; init; }

    public double? Z { get; init; }

    public double? I { get; init; }

    public double? J { get; init; }

    public double? K { get; init; }

    public double? F { get; init; }

    public double? S { get; init; }

    public int? T { get; init; }

    public int? H { get; init; }

    public int? D { get; init; }

    public int? N { get; init; }

    public IReadOnlyList<GCodeDiagnostic> Diagnostics { get; init; } = [];

    public bool HasErrors => Diagnostics.Any(d => d.IsError);

    public bool HasAxisWords => X.HasValue || Y.HasValue || Z.HasValue;

    public bool HasArcOffsets => I.HasValue || J.HasValue || K.HasValue;

    /// <summary>True for blank lines, comment-only lines and <c>%</c> delimiters.</summary>
    public bool IsEmpty =>
        GFunctions.Count == 0 && MFunctions.Count == 0 && !HasAxisWords && !HasArcOffsets
        && F is null && S is null && T is null && H is null && D is null;

    public GFunction? GetGFunction(ModalGroup group)
    {
        foreach (var code in GFunctions)
        {
            if (CodeCatalog.GetGroup(code) == group)
            {
                return code;
            }
        }

        return null;
    }

    public MFunction? GetMFunction(ModalGroup group)
    {
        foreach (var code in MFunctions)
        {
            if (CodeCatalog.GetGroup(code) == group)
            {
                return code;
            }
        }

        return null;
    }

    public bool Has(GFunction code) => GFunctions.Contains(code);

    public override string ToString() => $"{LineNumber}: {Text}";
}
