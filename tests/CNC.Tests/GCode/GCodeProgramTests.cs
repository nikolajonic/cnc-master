using System.Diagnostics;
using System.Text;
using CNC.Core.Geometry;
using CNC.GCode;
using CNC.GCode.Commands;

namespace CNC.Tests.GCode;

public sealed class GCodeProgramTests
{
    private const string SquareProgram = """
        G21
        G90
        G0 X0 Y0
        G0 Z5
        G1 Z-1 F100
        G1 X50 F500
        G1 Y50
        G1 X0
        G1 Y0
        G0 Z5
        M5
        M30
        """;

    [Fact]
    public void SquareExample_ProducesExpectedCommands()
    {
        var program = GCodeProgram.Parse(SquareProgram, name: "square.nc");

        Assert.False(program.HasErrors, string.Join(Environment.NewLine, program.Diagnostics));
        Assert.Empty(program.Warnings);
        Assert.Equal("square.nc", program.Name);
        Assert.Equal(12, program.Lines.Count);

        var moves = program.Commands.OfType<MotionCommand>().ToList();
        Assert.Equal(
            [
                new Position(0, 0, 0),
                new Position(0, 0, 5),
                new Position(0, 0, -1),
                new Position(50, 0, -1),
                new Position(50, 50, -1),
                new Position(0, 50, -1),
                new Position(0, 0, -1),
                new Position(0, 0, 5),
            ],
            moves.Select(m => m.Target));
        Assert.Equal([3, 4, 5, 6, 7, 8, 9, 10], moves.Select(m => m.LineNumber));
        Assert.Equal(100, Assert.IsType<LinearMoveCommand>(moves[2]).FeedRate);
        Assert.All(moves.Skip(3).Take(4), m => Assert.Equal(500, Assert.IsType<LinearMoveCommand>(m).FeedRate));

        Assert.IsType<StopSpindleCommand>(program.Commands[^2]);
        Assert.Equal(ProgramStopKind.EndAndRewind, Assert.IsType<ProgramStopCommand>(program.Commands[^1]).Kind);
        Assert.True(program.FinalState.ProgramEnded);
    }

    [Fact]
    public void Diagnostics_ReportLineNumberOffendingTextAndMessage()
    {
        var program = GCodeProgram.Parse("G21\nG0 X0\nG5 X1\nM30");

        var error = Assert.Single(program.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("G5", error.OffendingText);
        Assert.Equal("Error - Line 3, column 1 'G5': Unsupported G-code G5.", error.ToString());
    }

    [Fact]
    public void AllErrorsInProgram_AreReported()
    {
        var program = GCodeProgram.Parse("G5\nG1 X1\nX2 X3\nM30");

        Assert.Equal([1, 2, 3], program.Errors.Select(e => e.LineNumber));
    }

    [Fact]
    public void MissingProgramEnd_IsWarning()
    {
        var program = GCodeProgram.Parse("G0 X1\n");

        Assert.False(program.HasErrors);
        Assert.Contains(program.Warnings, w => w.Message.Contains("M2 or M30", StringComparison.Ordinal));
    }

    [Fact]
    public void LineEndings_AreHandledUniformly()
    {
        var program = GCodeProgram.Parse("G0 X1\r\nG0 X2\rG0 X3\nM30\r\n");

        Assert.Equal(4, program.Lines.Count);
        Assert.Equal([1, 2, 3], program.Commands.OfType<MotionCommand>().Select(m => m.LineNumber));
    }

    [Fact]
    public void CommentsAndBlankLines_AreSkipped()
    {
        var program = GCodeProgram.Parse("%\n(header)\n\n; note\nG0 X1 (move)\nM30\n%");

        Assert.False(program.HasErrors);
        Assert.Equal(5, Assert.IsType<RapidMoveCommand>(program.Commands[0]).LineNumber);
    }

    [Fact]
    public void EmptyText_ProducesEmptyProgram()
    {
        var program = GCodeProgram.Parse(string.Empty);

        Assert.Empty(program.Lines);
        Assert.Empty(program.Commands);
        Assert.Empty(program.Diagnostics);
    }

    [Fact]
    public void LargeProgram_ParsesQuickly()
    {
        var text = new StringBuilder("G21 G90 G1 F1000\n");
        for (var i = 0; i < 100_000; i++)
        {
            text.Append('X').Append(i % 500).Append(" Y").Append(i % 300).Append('\n');
        }

        text.Append("M30\n");

        var stopwatch = Stopwatch.StartNew();
        var program = GCodeProgram.Parse(text.ToString());
        stopwatch.Stop();

        Assert.False(program.HasErrors);
        Assert.Equal(100_001, program.Commands.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Parsing took {stopwatch.Elapsed}.");
    }
}
