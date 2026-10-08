namespace CNC.Core.Machine;

/// <summary>The complete table of permitted machine state transitions.</summary>
public static class MachineStateTransitions
{
    public static bool IsAllowed(MachineState from, MachineState to)
    {
        if (from == to)
        {
            return false;
        }

        return to switch
        {
            // Emergency stop and loss of connection must always be representable.
            MachineState.EmergencyStop => true,
            MachineState.Disconnected => true,

            // An emergency stop dominates an alarm and is only cleared by a reset.
            MachineState.Alarm => from is not (MachineState.Disconnected or MachineState.EmergencyStop),

            MachineState.Resetting => from is MachineState.EmergencyStop or MachineState.Alarm,

            MachineState.Idle => from is MachineState.Disconnected
                or MachineState.Ready
                or MachineState.Running
                or MachineState.Stopping
                or MachineState.Jogging
                or MachineState.Homing
                or MachineState.Resetting,

            // Ready means referenced, so it is never entered straight from Idle or Disconnected; homing does that.
            MachineState.Ready => from is MachineState.Running
                or MachineState.Stopping
                or MachineState.Jogging
                or MachineState.Homing
                or MachineState.Resetting,

            MachineState.Running => from is MachineState.Idle or MachineState.Ready or MachineState.Paused,

            MachineState.Paused => from is MachineState.Running,

            MachineState.Stopping => from is MachineState.Running
                or MachineState.Paused
                or MachineState.Jogging
                or MachineState.Homing,

            MachineState.Homing => from is MachineState.Idle or MachineState.Ready,

            MachineState.Jogging => from is MachineState.Idle or MachineState.Ready,

            _ => false,
        };
    }
}
