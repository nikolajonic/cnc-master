using CNC.GCode.Diagnostics;
using CNC.GCode.Modal;
using CNC.GCode.Parsing;

namespace CNC.GCode.Interpretation;

/// <summary>
/// Executes parsed blocks against the modal state and produces typed commands. A block that
/// contains an error produces no commands and leaves the state untouched, so interpretation can
/// continue and report every problem in the program.
/// </summary>
public sealed class GCodeInterpreter
{
    private readonly InterpreterOptions _options;
    private bool _reportedIgnoredAfterEnd;

    public GCodeInterpreter(InterpreterOptions? options = null)
    {
        _options = options ?? new InterpreterOptions();
        State = new ModalState
        {
            Units = _options.InitialUnits,
            Position = _options.InitialMachinePosition - _options.GetWorkOffset(Core.Coordinates.WorkCoordinateSystem.G54),
        };
    }

    public ModalState State { get; private set; }

    public BlockResult Execute(GCodeBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        // Syntax errors were already reported by the parser.
        if (block.HasErrors || block.IsEmpty || (block.IsBlockDelete && _options.BlockDeleteEnabled))
        {
            return BlockResult.Empty;
        }

        if (State.ProgramEnded)
        {
            if (_reportedIgnoredAfterEnd)
            {
                return BlockResult.Empty;
            }

            _reportedIgnoredAfterEnd = true;
            return new BlockResult([], [GCodeDiagnostic.Warning(block.LineNumber, 0, block.Text.Trim(),
                "Lines after the program end (M2/M30) are ignored.")]);
        }

        var execution = new BlockExecution(block, State, _options);
        execution.Run();

        if (execution.HasErrors)
        {
            return new BlockResult([], execution.Diagnostics);
        }

        State = execution.State;
        return new BlockResult(execution.Commands, execution.Diagnostics);
    }
}
