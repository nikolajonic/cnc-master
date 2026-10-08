using CNC.Core.Axes;

namespace CNC.Hardware.Motion;

public enum MotionCompletion
{
    /// <summary>The whole plan was executed.</summary>
    Completed,

    /// <summary>A controlled stop ended the plan early.</summary>
    Stopped,

    /// <summary>An emergency stop or disconnect aborted motion without deceleration.</summary>
    Aborted,

    /// <summary>A fault aborted motion.</summary>
    Faulted,
}

public enum MotionFaultKind
{
    SoftLimit,
    LimitSwitch,
    CommunicationLost,
    ControllerError,
}

public sealed record MotionFault(MotionFaultKind Kind, string Message, Axis? Axis = null);

public sealed class MotionStatusChangedEventArgs(MotionStatus status) : EventArgs
{
    public MotionStatus Status { get; } = status;
}

public sealed class MotionCompletedEventArgs(MotionCompletion completion, int lastSourceLine) : EventArgs
{
    public MotionCompletion Completion { get; } = completion;

    public int LastSourceLine { get; } = lastSourceLine;
}

public sealed class MotionFaultEventArgs(MotionFault fault) : EventArgs
{
    public MotionFault Fault { get; } = fault;
}
