using CNC.Core.Geometry;

namespace CNC.Hardware.Motion;

public enum ControllerMode
{
    Simulation,
    Hardware,
}

public enum MotionState
{
    Disconnected,
    Idle,
    Running,

    /// <summary>Feed hold requested; decelerating.</summary>
    Holding,

    /// <summary>Feed hold complete; motion can resume.</summary>
    Held,

    /// <summary>Controlled stop in progress; the remaining motion is discarded.</summary>
    Stopping,
    EmergencyStop,

    /// <summary>Motion was aborted by a fault (for example a travel limit). Requires a reset.</summary>
    Fault,
}

/// <summary>Snapshot of the controller's motion state. Positions are machine coordinates.</summary>
public sealed record MotionStatus(
    MotionState State,
    Position MachinePosition,
    Position Velocity,
    double FeedOverride,
    int ActiveSourceLine,
    int SegmentIndex,
    int SegmentCount,
    double Progress)
{
    /// <summary>Path speed in units per minute.</summary>
    public double FeedRate => Velocity.Length * 60.0;

    public bool IsMoving => State is MotionState.Running or MotionState.Holding or MotionState.Stopping;

    /// <summary>A motion plan is loaded and has not finished (running, holding, held or stopping).</summary>
    public bool IsBusy => IsMoving || State == MotionState.Held;
}
