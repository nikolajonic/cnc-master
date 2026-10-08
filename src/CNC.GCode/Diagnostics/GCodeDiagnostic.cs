namespace CNC.GCode.Diagnostics;

public enum DiagnosticSeverity
{
    /// <summary>The line is understood but something is ignored or unusual.</summary>
    Warning,

    /// <summary>The line cannot be executed safely. A program with errors must not run.</summary>
    Error,
}

/// <param name="LineNumber">1-based source line.</param>
/// <param name="Column">1-based column of the offending text, or 0 when it applies to the whole line.</param>
/// <param name="OffendingText">The word or text that caused the problem, for example <c>G5</c>.</param>
public sealed record GCodeDiagnostic(
    DiagnosticSeverity Severity,
    int LineNumber,
    int Column,
    string OffendingText,
    string Message)
{
    public static GCodeDiagnostic Error(int line, int column, string offendingText, string message) =>
        new(DiagnosticSeverity.Error, line, column, offendingText, message);

    public static GCodeDiagnostic Warning(int line, int column, string offendingText, string message) =>
        new(DiagnosticSeverity.Warning, line, column, offendingText, message);

    public bool IsError => Severity == DiagnosticSeverity.Error;

    public override string ToString()
    {
        var where = Column > 0 ? $"Line {LineNumber}, column {Column}" : $"Line {LineNumber}";
        var what = string.IsNullOrEmpty(OffendingText) ? string.Empty : $" '{OffendingText}'";
        return $"{Severity} - {where}{what}: {Message}";
    }
}
