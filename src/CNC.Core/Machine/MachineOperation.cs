namespace CNC.Core.Machine;

/// <summary>Operator-level actions whose availability depends on the machine state.</summary>
public enum MachineOperation
{
    EmergencyStop,
    LoadProgram,
    StartProgram,
    PauseProgram,
    ResumeProgram,
    StopMotion,
    Jog,
    Home,
    Reset,
    SetWorkOffset,
    ControlSpindle,
    EditConfiguration,
}
