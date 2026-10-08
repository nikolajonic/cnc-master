namespace CNC.Core.Configuration;

public enum ConfigurationLoadStatus
{
    /// <summary>The stored configuration was read and is valid.</summary>
    Loaded,

    /// <summary>No configuration existed; defaults were created.</summary>
    CreatedDefault,

    /// <summary>
    /// The stored configuration could not be used. It was preserved as a backup and the defaults
    /// were loaded instead. Hardware must not be driven until the operator has reviewed the settings.
    /// </summary>
    RecoveredWithDefaults,
}

public sealed record ConfigurationLoadResult(
    MachineConfiguration Configuration,
    ConfigurationLoadStatus Status,
    string? Message = null);
