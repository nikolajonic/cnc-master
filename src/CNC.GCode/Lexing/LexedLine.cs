using CNC.GCode.Diagnostics;

namespace CNC.GCode.Lexing;

public sealed record LexedLine(
    int LineNumber,
    string Text,
    IReadOnlyList<GCodeWord> Words,
    string? Comment,
    bool IsBlockDelete,
    bool IsProgramDelimiter,
    IReadOnlyList<GCodeDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(d => d.IsError);
}
