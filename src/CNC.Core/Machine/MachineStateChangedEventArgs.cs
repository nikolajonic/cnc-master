namespace CNC.Core.Machine;

public sealed class MachineStateChangedEventArgs : EventArgs
{
    public MachineStateChangedEventArgs(MachineState previous, MachineState current, string reason)
    {
        Previous = previous;
        Current = current;
        Reason = reason;
    }

    public MachineState Previous { get; }

    public MachineState Current { get; }

    public string Reason { get; }
}

public sealed class MachineTransitionRejectedEventArgs : EventArgs
{
    public MachineTransitionRejectedEventArgs(MachineState current, MachineState requested, string reason)
    {
        Current = current;
        Requested = requested;
        Reason = reason;
    }

    public MachineState Current { get; }

    public MachineState Requested { get; }

    public string Reason { get; }
}
