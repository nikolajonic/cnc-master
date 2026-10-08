using System.Globalization;
using CNC.GCode.Codes;
using CNC.GCode.Diagnostics;
using CNC.GCode.Lexing;

namespace CNC.GCode.Parsing;

/// <summary>
/// Turns lexed words into a <see cref="GCodeBlock"/>, checking everything that can be decided
/// from the line alone: known codes, one code per modal group, no repeated words, valid values.
/// </summary>
public static class GCodeBlockParser
{
    public static GCodeBlock Parse(string text, int lineNumber) => Parse(GCodeLexer.Tokenize(text, lineNumber));

    public static GCodeBlock Parse(LexedLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var diagnostics = new List<GCodeDiagnostic>(line.Diagnostics);
        var gFunctions = new List<GFunction>();
        var mFunctions = new List<MFunction>();
        var groupOwners = new Dictionary<ModalGroup, GCodeWord>();
        var values = new Dictionary<char, GCodeWord>();

        foreach (var word in line.Words)
        {
            switch (word.Letter)
            {
                case 'G':
                    AddGFunction(word, line.LineNumber, gFunctions, groupOwners, diagnostics);
                    break;
                case 'M':
                    AddMFunction(word, line.LineNumber, mFunctions, groupOwners, diagnostics);
                    break;
                case 'X' or 'Y' or 'Z' or 'I' or 'J' or 'K' or 'F' or 'S' or 'T' or 'H' or 'D' or 'N':
                    AddValue(word, line.LineNumber, values, diagnostics);
                    break;
                default:
                    diagnostics.Add(GCodeDiagnostic.Error(line.LineNumber, word.Column, word.Text, DescribeUnsupportedWord(word.Letter)));
                    break;
            }
        }

        return new GCodeBlock
        {
            LineNumber = line.LineNumber,
            Text = line.Text,
            Comment = line.Comment,
            IsBlockDelete = line.IsBlockDelete,
            IsProgramDelimiter = line.IsProgramDelimiter,
            GFunctions = gFunctions,
            MFunctions = mFunctions,
            X = Value(values, 'X'),
            Y = Value(values, 'Y'),
            Z = Value(values, 'Z'),
            I = Value(values, 'I'),
            J = Value(values, 'J'),
            K = Value(values, 'K'),
            F = Value(values, 'F'),
            S = Value(values, 'S'),
            T = IntValue(values, 'T'),
            H = IntValue(values, 'H'),
            D = IntValue(values, 'D'),
            N = IntValue(values, 'N'),
            Diagnostics = diagnostics,
        };
    }

    private static void AddGFunction(
        GCodeWord word, int lineNumber, List<GFunction> codes, Dictionary<ModalGroup, GCodeWord> groupOwners, List<GCodeDiagnostic> diagnostics)
    {
        if (!CodeCatalog.TryGetGFunction(word.Value, out var code))
        {
            diagnostics.Add(GCodeDiagnostic.Error(lineNumber, word.Column, word.Text,
                string.Create(CultureInfo.InvariantCulture, $"Unsupported G-code G{word.Value:0.####}.")));
            return;
        }

        var group = CodeCatalog.GetGroup(code);
        if (groupOwners.TryGetValue(group, out var existing))
        {
            diagnostics.Add(GCodeDiagnostic.Error(lineNumber, word.Column, word.Text,
                $"{word.Text} conflicts with {existing.Text}: both belong to the {Describe(group)} group."));
            return;
        }

        groupOwners[group] = word;
        codes.Add(code);
    }

    private static void AddMFunction(
        GCodeWord word, int lineNumber, List<MFunction> codes, Dictionary<ModalGroup, GCodeWord> groupOwners, List<GCodeDiagnostic> diagnostics)
    {
        if (!CodeCatalog.TryGetMFunction(word.Value, out var code))
        {
            // Auxiliary functions such as coolant (M7/M8/M9) do not affect motion, so they are
            // reported and skipped rather than rejecting the whole program.
            diagnostics.Add(GCodeDiagnostic.Warning(lineNumber, word.Column, word.Text,
                string.Create(CultureInfo.InvariantCulture, $"Unsupported M-code M{word.Value:0.####} is ignored.")));
            return;
        }

        var group = CodeCatalog.GetGroup(code);
        if (groupOwners.TryGetValue(group, out var existing))
        {
            diagnostics.Add(GCodeDiagnostic.Error(lineNumber, word.Column, word.Text,
                $"{word.Text} conflicts with {existing.Text}: both belong to the {Describe(group)} group."));
            return;
        }

        groupOwners[group] = word;
        codes.Add(code);
    }

    private static void AddValue(GCodeWord word, int lineNumber, Dictionary<char, GCodeWord> values, List<GCodeDiagnostic> diagnostics)
    {
        if (values.TryGetValue(word.Letter, out var existing))
        {
            diagnostics.Add(GCodeDiagnostic.Error(lineNumber, word.Column, word.Text,
                $"Word '{word.Letter}' appears more than once in the block (first: {existing.Text})."));
            return;
        }

        var error = word.Letter switch
        {
            'T' or 'H' or 'D' or 'N' when !IsNonNegativeInteger(word.Value) => $"'{word.Letter}' must be a non-negative whole number.",
            'F' when word.Value < 0 => "Feed rate must not be negative.",
            'S' when word.Value < 0 => "Spindle speed must not be negative.",
            _ => null,
        };

        if (error is not null)
        {
            diagnostics.Add(GCodeDiagnostic.Error(lineNumber, word.Column, word.Text, error));
            return;
        }

        values[word.Letter] = word;
    }

    private static string DescribeUnsupportedWord(char letter) => letter switch
    {
        'A' or 'B' or 'C' => $"Rotary axis {letter} is not supported.",
        'R' => "R-format arcs are not supported; use I/J/K center offsets.",
        _ => $"Word '{letter}' is not supported.",
    };

    private static string Describe(ModalGroup group) => group switch
    {
        ModalGroup.NonModal => "non-modal",
        ModalGroup.Motion => "motion",
        ModalGroup.Plane => "plane selection",
        ModalGroup.DistanceMode => "distance mode",
        ModalGroup.FeedRateMode => "feed rate mode",
        ModalGroup.Units => "units",
        ModalGroup.CutterCompensation => "cutter compensation",
        ModalGroup.ToolLengthOffset => "tool length offset",
        ModalGroup.CoordinateSystem => "coordinate system",
        ModalGroup.Stopping => "program stop",
        ModalGroup.ToolChange => "tool change",
        ModalGroup.Spindle => "spindle",
        _ => group.ToString(),
    };

    private static bool IsNonNegativeInteger(double value) => value >= 0 && value == Math.Floor(value) && value <= int.MaxValue;

    private static double? Value(Dictionary<char, GCodeWord> values, char letter) =>
        values.TryGetValue(letter, out var word) ? word.Value : null;

    private static int? IntValue(Dictionary<char, GCodeWord> values, char letter) =>
        values.TryGetValue(letter, out var word) ? (int)word.Value : null;
}
