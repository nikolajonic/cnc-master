using CNC.Core.Common;
using CNC.Motion.Planning;

namespace CNC.Hardware.Motion;

/// <summary>
/// Contract shared by the simulator and real motion controllers. The UI and the program executor
/// talk only to this interface, so switching between simulation and hardware does not change them.
/// </summary>
/// <remarks>
/// Software emergency stop is a convenience, not a safety function: real machines must also have
/// an independent, hard-wired emergency stop circuit.
/// Events may be raised on a background thread.
/// </remarks>
public interface IMotionController : IDisposable, IAsyncDisposable
{
    string Name { get; }

    ControllerMode Mode { get; }

    bool IsConnected { get; }

    MotionStatus Status { get; }

    /// <summary>Raised when the state changes and periodically while moving.</summary>
    event EventHandler<MotionStatusChangedEventArgs>? StatusChanged;

    /// <summary>Raised once when an executed plan finishes, is stopped or is aborted.</summary>
    event EventHandler<MotionCompletedEventArgs>? MotionCompleted;

    event EventHandler<MotionFaultEventArgs>? FaultRaised;

    Task<Result> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Disconnects; any motion in progress is aborted first.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts executing a plan. The plan must start at the current machine position.</summary>
    Result Execute(MotionPlan plan);

    /// <summary>Decelerates along the path and holds position until <see cref="ResumeMotion"/>.</summary>
    Result FeedHold();

    Result ResumeMotion();

    /// <summary>Decelerates along the path and discards the rest of the plan.</summary>
    Result StopMotion();

    /// <summary>Immediately stops all motion. Always accepted, even when disconnected.</summary>
    void EmergencyStop();

    /// <summary>Clears an emergency stop or fault so motion can be commanded again.</summary>
    Result Reset();

    /// <summary>Feed override as a factor (1.0 = 100 %). Rapid moves are not affected.</summary>
    Result SetFeedOverride(double factor);
}
