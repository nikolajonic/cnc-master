using CNC.Core.Common;

namespace CNC.Core.Machine;

public sealed class MachineStateMachine : IMachineStateMachine
{
    private readonly object _gate = new();
    private MachineState _current;

    public MachineStateMachine(MachineState initialState = MachineState.Disconnected)
    {
        _current = initialState;
    }

    public event EventHandler<MachineStateChangedEventArgs>? StateChanged;

    public event EventHandler<MachineTransitionRejectedEventArgs>? TransitionRejected;

    public MachineState Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public bool CanTransitionTo(MachineState target)
    {
        lock (_gate)
        {
            return MachineStateTransitions.IsAllowed(_current, target);
        }
    }

    public Result TryTransitionTo(MachineState target, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        // Events are raised under the lock so that observers always see transitions in order.
        lock (_gate)
        {
            var previous = _current;
            if (!MachineStateTransitions.IsAllowed(previous, target))
            {
                TransitionRejected?.Invoke(this, new MachineTransitionRejectedEventArgs(previous, target, reason));
                return Result.Failure($"Transition from {previous} to {target} is not allowed.");
            }

            _current = target;
            StateChanged?.Invoke(this, new MachineStateChangedEventArgs(previous, target, reason));
            return Result.Success();
        }
    }

    public Result CanPerform(MachineOperation operation) => MachineOperationRules.Check(Current, operation);
}
