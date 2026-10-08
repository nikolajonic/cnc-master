using CNC.Core.Common;

namespace CNC.Core.Machine;

/// <summary>
/// Single source of truth for the machine state. Controllers drive it; the UI observes it.
/// </summary>
public interface IMachineStateMachine
{
    MachineState Current { get; }

    /// <summary>
    /// Raised synchronously on the thread that performed the transition, in transition order.
    /// Handlers must be fast, must not block, and must not request another transition synchronously.
    /// </summary>
    event EventHandler<MachineStateChangedEventArgs>? StateChanged;

    event EventHandler<MachineTransitionRejectedEventArgs>? TransitionRejected;

    bool CanTransitionTo(MachineState target);

    /// <param name="reason">Why the transition is requested; recorded in the log.</param>
    Result TryTransitionTo(MachineState target, string reason);

    /// <summary>Checks whether <paramref name="operation"/> is permitted in the current state.</summary>
    Result CanPerform(MachineOperation operation);
}
