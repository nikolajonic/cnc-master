using CNC.Core.Common;

namespace CNC.Core.Machine;

/// <summary>
/// Which operator actions are permitted in which state. The UI uses this to enable controls and
/// controllers use it to reject commands, so both apply the same rules.
/// </summary>
public static class MachineOperationRules
{
    public static bool IsAllowed(MachineState state, MachineOperation operation) => operation switch
    {
        MachineOperation.EmergencyStop => true,

        MachineOperation.LoadProgram => state is MachineState.Disconnected
            or MachineState.Idle
            or MachineState.Ready
            or MachineState.Alarm
            or MachineState.EmergencyStop,

        MachineOperation.StartProgram => IsIdle(state),
        MachineOperation.PauseProgram => state is MachineState.Running,
        MachineOperation.ResumeProgram => state is MachineState.Paused,

        MachineOperation.StopMotion => state is MachineState.Running
            or MachineState.Paused
            or MachineState.Jogging
            or MachineState.Homing,

        // Jogging is included so incremental jogs can be queued while a previous step finishes.
        MachineOperation.Jog => IsIdle(state) || state is MachineState.Jogging,

        MachineOperation.Home => IsIdle(state),
        MachineOperation.Reset => state is MachineState.EmergencyStop or MachineState.Alarm,
        MachineOperation.SetWorkOffset => IsIdle(state),
        MachineOperation.ControlSpindle => IsIdle(state),

        MachineOperation.EditConfiguration => state is MachineState.Disconnected
            or MachineState.Idle
            or MachineState.Ready
            or MachineState.Alarm
            or MachineState.EmergencyStop,

        _ => false,
    };

    public static Result Check(MachineState state, MachineOperation operation) =>
        IsAllowed(state, operation)
            ? Result.Success()
            : Result.Failure($"{Describe(operation)} is not allowed while the machine is in the {state} state.");

    private static bool IsIdle(MachineState state) => state is MachineState.Idle or MachineState.Ready;

    private static string Describe(MachineOperation operation) => operation switch
    {
        MachineOperation.EmergencyStop => "Emergency stop",
        MachineOperation.LoadProgram => "Loading a program",
        MachineOperation.StartProgram => "Starting a program",
        MachineOperation.PauseProgram => "Pausing the program",
        MachineOperation.ResumeProgram => "Resuming the program",
        MachineOperation.StopMotion => "Stopping motion",
        MachineOperation.Jog => "Jogging",
        MachineOperation.Home => "Homing",
        MachineOperation.Reset => "Reset",
        MachineOperation.SetWorkOffset => "Changing work offsets",
        MachineOperation.ControlSpindle => "Manual spindle control",
        MachineOperation.EditConfiguration => "Editing the machine configuration",
        _ => operation.ToString(),
    };
}
