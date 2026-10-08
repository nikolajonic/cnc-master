namespace CNC.Core.Machine;

public enum MachineState
{
    /// <summary>No controller connection. Nothing can move.</summary>
    Disconnected,

    /// <summary>Connected and able to move, but the machine has not been referenced (homed).</summary>
    Idle,

    /// <summary>Connected and referenced; machine coordinates are trustworthy.</summary>
    Ready,

    /// <summary>A program is executing.</summary>
    Running,

    /// <summary>Program execution is suspended by feed hold or pause; it can be resumed.</summary>
    Paused,

    /// <summary>A controlled stop is in progress; motion is decelerating.</summary>
    Stopping,

    /// <summary>Emergency stop is active. All motion and the spindle are halted. Requires reset.</summary>
    EmergencyStop,

    /// <summary>A fault (limit switch, soft limit, controller error) halted the machine. Requires reset.</summary>
    Alarm,

    /// <summary>The homing cycle is running.</summary>
    Homing,

    /// <summary>A manual jog move is running.</summary>
    Jogging,

    /// <summary>The reset procedure that clears an emergency stop or alarm is running.</summary>
    Resetting,
}
