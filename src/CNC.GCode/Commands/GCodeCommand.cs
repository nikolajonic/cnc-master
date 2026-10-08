namespace CNC.GCode.Commands;

/// <summary>
/// A fully resolved instruction produced by the interpreter. All lengths are absolute millimetres
/// in the work coordinate system that was active for the block; feed rates are mm/min.
/// </summary>
/// <param name="LineNumber">1-based source line, used to highlight the executing line.</param>
public abstract record GCodeCommand(int LineNumber);
