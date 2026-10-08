using CNC.GCode.Commands;
using CNC.GCode.Diagnostics;
using CNC.GCode.Interpretation;
using CNC.GCode.Modal;
using CNC.GCode.Parsing;

namespace CNC.GCode;

/// <summary>
/// A G-code program after lexing, parsing and interpretation: the source lines for display, the
/// typed commands for execution and every diagnostic found. Programs with errors must not run.
/// </summary>
public sealed class GCodeProgram
{
    private GCodeProgram(
        string? name,
        IReadOnlyList<string> lines,
        IReadOnlyList<GCodeBlock> blocks,
        IReadOnlyList<GCodeCommand> commands,
        IReadOnlyList<GCodeDiagnostic> diagnostics,
        ModalState finalState)
    {
        Name = name;
        Lines = lines;
        Blocks = blocks;
        Commands = commands;
        Diagnostics = diagnostics;
        FinalState = finalState;
    }

    public string? Name { get; }

    public IReadOnlyList<string> Lines { get; }

    public IReadOnlyList<GCodeBlock> Blocks { get; }

    public IReadOnlyList<GCodeCommand> Commands { get; }

    public IReadOnlyList<GCodeDiagnostic> Diagnostics { get; }

    public ModalState FinalState { get; }

    public bool HasErrors => Diagnostics.Any(d => d.IsError);

    public IEnumerable<GCodeDiagnostic> Errors => Diagnostics.Where(d => d.IsError);

    public IEnumerable<GCodeDiagnostic> Warnings => Diagnostics.Where(d => !d.IsError);

    public static GCodeProgram Parse(string text, InterpreterOptions? options = null, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = SplitLines(text);
        var blocks = new List<GCodeBlock>(lines.Length);
        var commands = new List<GCodeCommand>(lines.Length);
        var diagnostics = new List<GCodeDiagnostic>();
        var interpreter = new GCodeInterpreter(options);

        for (var i = 0; i < lines.Length; i++)
        {
            var block = GCodeBlockParser.Parse(lines[i], i + 1);
            blocks.Add(block);
            diagnostics.AddRange(block.Diagnostics);

            var result = interpreter.Execute(block);
            commands.AddRange(result.Commands);
            diagnostics.AddRange(result.Diagnostics);
        }

        if (!interpreter.State.ProgramEnded && blocks.Any(b => !b.IsEmpty))
        {
            diagnostics.Add(GCodeDiagnostic.Warning(lines.Length, 0, string.Empty, "Program does not end with M2 or M30."));
        }

        return new GCodeProgram(name, lines, blocks, commands, diagnostics, interpreter.State);
    }

    private static string[] SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }
}
