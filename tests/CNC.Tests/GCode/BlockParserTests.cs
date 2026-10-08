using CNC.GCode.Codes;
using CNC.GCode.Diagnostics;
using CNC.GCode.Parsing;

namespace CNC.Tests.GCode;

public sealed class BlockParserTests
{
    [Fact]
    public void Parse_ProducesTypedCodesAndValues()
    {
        var block = GCodeBlockParser.Parse("N10 G17 G21 G1 X1 Y2 Z3 F500 S1000 T2 M3", 4);

        Assert.False(block.HasErrors);
        Assert.Equal([GFunction.G17, GFunction.G21, GFunction.G1], block.GFunctions);
        Assert.Equal([MFunction.M3], block.MFunctions);
        Assert.Equal((1d, 2d, 3d), (block.X!.Value, block.Y!.Value, block.Z!.Value));
        Assert.Equal(500, block.F);
        Assert.Equal(1000, block.S);
        Assert.Equal(2, block.T);
        Assert.Equal(10, block.N);
        Assert.Equal(GFunction.G1, block.GetGFunction(ModalGroup.Motion));
    }

    [Theory]
    [InlineData("G0", GFunction.G0)]
    [InlineData("G00", GFunction.G0)]
    [InlineData("G01", GFunction.G1)]
    [InlineData("G2", GFunction.G2)]
    [InlineData("G3", GFunction.G3)]
    [InlineData("G18", GFunction.G18)]
    [InlineData("G20", GFunction.G20)]
    [InlineData("G28", GFunction.G28)]
    [InlineData("G40", GFunction.G40)]
    [InlineData("G43 H1", GFunction.G43)]
    [InlineData("G59", GFunction.G59)]
    [InlineData("G80", GFunction.G80)]
    [InlineData("G91", GFunction.G91)]
    [InlineData("G94", GFunction.G94)]
    public void Parse_RecognisesSupportedGCodes(string text, GFunction expected)
    {
        var block = GCodeBlockParser.Parse(text, 1);

        Assert.False(block.HasErrors);
        Assert.Contains(expected, block.GFunctions);
    }

    [Theory]
    [InlineData("M0", MFunction.M0)]
    [InlineData("M1", MFunction.M1)]
    [InlineData("M2", MFunction.M2)]
    [InlineData("M03", MFunction.M3)]
    [InlineData("M4", MFunction.M4)]
    [InlineData("M5", MFunction.M5)]
    [InlineData("M6", MFunction.M6)]
    [InlineData("M30", MFunction.M30)]
    public void Parse_RecognisesSupportedMCodes(string text, MFunction expected)
    {
        Assert.Equal([expected], GCodeBlockParser.Parse(text, 1).MFunctions);
    }

    [Theory]
    [InlineData("G5", "Unsupported G-code G5")]
    [InlineData("G81 X1", "Unsupported G-code G81")]
    [InlineData("G17.1", "Unsupported G-code G17.1")]
    [InlineData("G4 P1", "Unsupported G-code G4")]
    public void Parse_UnsupportedGCode_IsErrorWithLineAndText(string text, string expectedMessage)
    {
        var block = GCodeBlockParser.Parse(text, 12);

        var error = Assert.Single(block.Diagnostics, d => d.Message.StartsWith("Unsupported G-code", StringComparison.Ordinal));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Equal(12, error.LineNumber);
        Assert.Equal(1, error.Column);
        Assert.Contains(expectedMessage, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_UnsupportedMCode_IsWarningAndIgnored()
    {
        var block = GCodeBlockParser.Parse("M8 G0 X1", 1);

        var warning = Assert.Single(block.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal("M8", warning.OffendingText);
        Assert.False(block.HasErrors);
        Assert.Empty(block.MFunctions);
    }

    [Theory]
    [InlineData("G0 G1 X1", "motion")]
    [InlineData("G90 G91", "distance mode")]
    [InlineData("G20 G21", "units")]
    [InlineData("G54 G55", "coordinate system")]
    [InlineData("G17 G18", "plane selection")]
    [InlineData("M3 M5", "spindle")]
    [InlineData("M0 M30", "program stop")]
    public void Parse_TwoCodesFromSameModalGroup_IsError(string text, string groupName)
    {
        var block = GCodeBlockParser.Parse(text, 1);

        var error = Assert.Single(block.Diagnostics);
        Assert.True(error.IsError);
        Assert.Contains(groupName, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_CodesFromDifferentGroups_AreAllowed()
    {
        var block = GCodeBlockParser.Parse("G90 G21 G17 G54 G0 M3 M6 T1", 1);

        Assert.False(block.HasErrors);
        Assert.Equal(5, block.GFunctions.Count);
        Assert.Equal(2, block.MFunctions.Count);
    }

    [Fact]
    public void Parse_DuplicateWord_IsError()
    {
        var block = GCodeBlockParser.Parse("G1 X1 X2", 1);

        var error = Assert.Single(block.Diagnostics);
        Assert.Equal("X2", error.OffendingText);
        Assert.Equal(1, block.X);
    }

    [Theory]
    [InlineData("T1.5", "whole number")]
    [InlineData("T-1", "whole number")]
    [InlineData("F-100", "Feed rate")]
    [InlineData("S-5", "Spindle speed")]
    [InlineData("A10", "Rotary axis A")]
    [InlineData("G2 X1 R5", "R-format arcs")]
    [InlineData("P1", "Word 'P' is not supported")]
    public void Parse_InvalidOrUnsupportedWords_AreErrors(string text, string expectedMessage)
    {
        var block = GCodeBlockParser.Parse(text, 1);

        Assert.Contains(block.Diagnostics, d => d.IsError && d.Message.Contains(expectedMessage, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("(comment)")]
    [InlineData("%")]
    [InlineData("N100")]
    public void Parse_NonExecutableLines_AreEmpty(string text)
    {
        Assert.True(GCodeBlockParser.Parse(text, 1).IsEmpty);
    }

    [Fact]
    public void Parse_LexerErrorsAreCarriedIntoBlock()
    {
        var block = GCodeBlockParser.Parse("G1 X1 (unterminated", 2);

        Assert.True(block.HasErrors);
    }

    [Fact]
    public void Format_RendersDecimalCodes()
    {
        Assert.Equal("G1", CodeCatalog.Format(GFunction.G1));
        Assert.Equal("G43", CodeCatalog.Format(GFunction.G43));
    }
}
