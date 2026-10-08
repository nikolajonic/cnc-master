namespace CNC.GCode.Lexing;

/// <summary>A letter followed by a number, for example <c>G1</c> or <c>X-12.5</c>.</summary>
/// <param name="Letter">Upper-case word letter.</param>
/// <param name="Text">The word as written in the source, used in diagnostics.</param>
/// <param name="Column">1-based column of the letter.</param>
public readonly record struct GCodeWord(char Letter, double Value, string Text, int Column)
{
    public override string ToString() => Text;
}
