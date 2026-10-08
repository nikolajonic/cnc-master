using System.Globalization;
using CNC.GCode.Diagnostics;

namespace CNC.GCode.Lexing;

/// <summary>
/// Splits one line of G-code into words and comments. Supports <c>( ... )</c> and <c>;</c>
/// comments, a leading <c>/</c> block-delete marker, <c>%</c> program delimiters, case-insensitive
/// letters and whitespace between a letter and its number. Parameters and expressions are not supported.
/// </summary>
public static class GCodeLexer
{
    public static LexedLine Tokenize(string text, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(text);

        var words = new List<GCodeWord>();
        var diagnostics = new List<GCodeDiagnostic>();
        var comments = new List<string>();
        var isBlockDelete = false;

        if (text.AsSpan().Trim().SequenceEqual("%"))
        {
            return new LexedLine(lineNumber, text, words, null, false, true, diagnostics);
        }

        var i = SkipWhitespace(text, 0);
        if (i < text.Length && text[i] == '/')
        {
            isBlockDelete = true;
            i++;
        }

        while (i < text.Length)
        {
            var c = text[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '(')
            {
                i = ReadParenthesisComment(text, i, lineNumber, comments, diagnostics);
            }
            else if (c == ';')
            {
                comments.Add(text[(i + 1)..].Trim());
                break;
            }
            else if (char.IsAsciiLetter(c))
            {
                i = ReadWord(text, i, lineNumber, words, diagnostics);
            }
            else
            {
                i = SkipUnexpected(text, i, lineNumber, diagnostics);
            }
        }

        var comment = comments.Count > 0 ? string.Join(" ", comments) : null;
        return new LexedLine(lineNumber, text, words, comment, isBlockDelete, false, diagnostics);
    }

    private static int ReadParenthesisComment(
        string text, int start, int lineNumber, List<string> comments, List<GCodeDiagnostic> diagnostics)
    {
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == ')')
            {
                comments.Add(text[(start + 1)..i].Trim());
                return i + 1;
            }

            if (text[i] == '(')
            {
                diagnostics.Add(GCodeDiagnostic.Error(lineNumber, i + 1, "(", "Nested comments are not allowed."));
                var close = text.IndexOf(')', i);
                return close < 0 ? text.Length : close + 1;
            }
        }

        diagnostics.Add(GCodeDiagnostic.Error(lineNumber, start + 1, text[start..].Trim(), "Comment is missing its closing ')'."));
        return text.Length;
    }

    private static int ReadWord(string text, int start, int lineNumber, List<GCodeWord> words, List<GCodeDiagnostic> diagnostics)
    {
        var letter = char.ToUpperInvariant(text[start]);
        var i = start + 1;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }

        var numberStart = i;
        if (i < text.Length && text[i] is '+' or '-')
        {
            i++;
        }

        var digits = 0;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            i++;
            digits++;
        }

        if (i < text.Length && text[i] == '.')
        {
            i++;
            while (i < text.Length && char.IsAsciiDigit(text[i]))
            {
                i++;
                digits++;
            }
        }

        if (digits == 0)
        {
            diagnostics.Add(GCodeDiagnostic.Error(lineNumber, start + 1, letter.ToString(),
                $"Word '{letter}' is missing a numeric value."));
            return Math.Max(numberStart, start + 1);
        }

        var number = text.AsSpan(numberStart, i - numberStart);
        var wordText = string.Concat(letter.ToString(), number);
        if (!double.TryParse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value))
        {
            diagnostics.Add(GCodeDiagnostic.Error(lineNumber, start + 1, wordText, "Number is out of range."));
            return i;
        }

        words.Add(new GCodeWord(letter, value, wordText, start + 1));
        return i;
    }

    private static int SkipUnexpected(string text, int start, int lineNumber, List<GCodeDiagnostic> diagnostics)
    {
        var i = start;
        while (i < text.Length && !char.IsWhiteSpace(text[i]) && !char.IsAsciiLetter(text[i]) && text[i] is not '(' and not ';')
        {
            i++;
        }

        var offending = text[start..i];
        var message = text[start] switch
        {
            '#' or '[' or ']' or '=' => "Parameters and expressions are not supported.",
            '%' => "'%' is only valid on a line by itself.",
            _ when char.IsAsciiDigit(text[start]) || text[start] is '.' or '-' or '+' => "Number without a word letter.",
            _ => $"Unexpected character '{text[start]}'.",
        };

        diagnostics.Add(GCodeDiagnostic.Error(lineNumber, start + 1, offending, message));
        return i;
    }

    private static int SkipWhitespace(string text, int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }
}
