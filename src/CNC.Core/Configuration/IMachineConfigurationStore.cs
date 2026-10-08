using CNC.Core.Common;

namespace CNC.Core.Configuration;

/// <summary>Durable storage for the machine configuration.</summary>
public interface IMachineConfigurationStore
{
    /// <summary>
    /// Loads the stored configuration. A missing file yields the defaults; an unreadable or invalid
    /// file is preserved as a backup and the defaults are returned with an explanatory status.
    /// </summary>
    Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates and saves. Invalid configurations are rejected and never written.</summary>
    Task<Result> SaveAsync(MachineConfiguration configuration, CancellationToken cancellationToken = default);
}
