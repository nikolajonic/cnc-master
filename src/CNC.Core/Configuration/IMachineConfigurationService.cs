using CNC.Core.Common;

namespace CNC.Core.Configuration;

/// <summary>Holds the active machine configuration and publishes changes to it.</summary>
public interface IMachineConfigurationService
{
    MachineConfiguration Current { get; }

    /// <summary>Outcome of the most recent load, or <c>null</c> before <see cref="LoadAsync"/> completes.</summary>
    ConfigurationLoadResult? LastLoadResult { get; }

    /// <summary>Raised on the thread that applied the change.</summary>
    event EventHandler<MachineConfigurationChangedEventArgs>? Changed;

    Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates, persists, and then activates <paramref name="configuration"/>.</summary>
    Task<Result> UpdateAsync(MachineConfiguration configuration, CancellationToken cancellationToken = default);
}

public sealed class MachineConfigurationChangedEventArgs : EventArgs
{
    public MachineConfigurationChangedEventArgs(MachineConfiguration previous, MachineConfiguration current)
    {
        Previous = previous;
        Current = current;
    }

    public MachineConfiguration Previous { get; }

    public MachineConfiguration Current { get; }
}
