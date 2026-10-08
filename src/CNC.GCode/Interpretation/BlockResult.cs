using CNC.GCode.Commands;
using CNC.GCode.Diagnostics;

namespace CNC.GCode.Interpretation;

public sealed record BlockResult(IReadOnlyList<GCodeCommand> Commands, IReadOnlyList<GCodeDiagnostic> Diagnostics)
{
    public static BlockResult Empty { get; } = new([], []);

    public bool HasErrors => Diagnostics.Any(d => d.IsError);
}
