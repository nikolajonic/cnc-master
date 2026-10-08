using CNC.GCode.Lexing;

namespace CNC.Tests.GCode;

public sealed class LexerTests
{
    [Fact]
    public void Tokenize_SplitsWordsWithColumns()
    {
        var line = GCodeLexer.Tokenize("G1 X10.5 Y-2", 3);

        Assert.Empty(line.Diagnostics);
        Assert.Equal(3, line.LineNumber);
        Assert.Collection(
            line.Words,
            w => AssertWord(w, 'G', 1, 1),
            w => AssertWord(w, 'X', 10.5, 4),
            w => AssertWord(w, 'Y', -2, 10));
    }

    [Theory]
    [InlineData("X.5", 0.5)]
    [InlineData("X-.5", -0.5)]
    [InlineData("X+5", 5)]
    [InlineData("X5.", 5)]
    [InlineData("X 12", 12)]
    [InlineData("x007", 7)]
    public void Tokenize_AcceptsNumberForms(string text, double expected)
    {
        var word = Assert.Single(GCodeLexer.Tokenize(text, 1).Words);

        Assert.Equal('X', word.Letter);
        Assert.Equal(expected, word.Value);
    }

    [Fact]
    public void Tokenize_HandlesWordsWithoutSpaces()
    {
        var line = GCodeLexer.Tokenize("G01X10Y20F500", 1);

        Assert.Equal("GXYF", new string(line.Words.Select(w => w.Letter).ToArray()));
    }

    [Fact]
    public void Tokenize_IsCultureIndependent()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal(1.25, Assert.Single(GCodeLexer.Tokenize("X1.25", 1).Words).Value);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void Tokenize_CapturesParenthesisAndSemicolonComments()
    {
        var line = GCodeLexer.Tokenize("G0 (rapid) X1 ; to start", 1);

        Assert.Equal(2, line.Words.Count);
        Assert.Equal("rapid to start", line.Comment);
        Assert.Empty(line.Diagnostics);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("(only a comment)")]
    [InlineData("; only a comment")]
    public void Tokenize_BlankAndCommentLinesHaveNoWords(string text)
    {
        var line = GCodeLexer.Tokenize(text, 1);

        Assert.Empty(line.Words);
        Assert.Empty(line.Diagnostics);
    }

    [Fact]
    public void Tokenize_UnclosedComment_IsError()
    {
        var line = GCodeLexer.Tokenize("G0 X1 (oops", 7);

        var error = Assert.Single(line.Diagnostics);
        Assert.True(error.IsError);
        Assert.Equal(7, error.LineNumber);
        Assert.Equal(7, error.Column);
        Assert.Contains("closing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tokenize_NestedComment_IsError()
    {
        var line = GCodeLexer.Tokenize("(a (b) c)", 1);

        Assert.Contains(line.Diagnostics, d => d.Message.Contains("Nested", StringComparison.Ordinal));
    }

    [Fact]
    public void Tokenize_LetterWithoutNumber_IsError()
    {
        var line = GCodeLexer.Tokenize("G1 X Y5", 1);

        var error = Assert.Single(line.Diagnostics);
        Assert.Equal("X", error.OffendingText);
        Assert.Equal(4, error.Column);
        Assert.Equal(2, line.Words.Count);
    }

    [Theory]
    [InlineData("#1=5", "Parameters")]
    [InlineData("G1 X[1+2]", "Parameters")]
    [InlineData("G1 X1 $", "Unexpected character")]
    [InlineData("10 G1", "Number without a word letter")]
    public void Tokenize_UnsupportedSyntax_IsError(string text, string expectedMessage)
    {
        var line = GCodeLexer.Tokenize(text, 1);

        Assert.Contains(line.Diagnostics, d => d.IsError && d.Message.Contains(expectedMessage, StringComparison.Ordinal));
    }

    [Fact]
    public void Tokenize_LeadingSlash_MarksBlockDelete()
    {
        var line = GCodeLexer.Tokenize("  /G0 X1", 1);

        Assert.True(line.IsBlockDelete);
        Assert.Equal(2, line.Words.Count);
    }

    [Fact]
    public void Tokenize_PercentLine_IsProgramDelimiter()
    {
        var line = GCodeLexer.Tokenize(" % ", 1);

        Assert.True(line.IsProgramDelimiter);
        Assert.Empty(line.Diagnostics);
    }

    private static void AssertWord(GCodeWord word, char letter, double value, int column)
    {
        Assert.Equal(letter, word.Letter);
        Assert.Equal(value, word.Value);
        Assert.Equal(column, word.Column);
    }
}
