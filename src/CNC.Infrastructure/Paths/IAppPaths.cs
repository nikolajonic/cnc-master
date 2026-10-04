namespace CNC.Infrastructure.Paths;

/// <summary>Well-known locations for application data written at runtime.</summary>
public interface IAppPaths
{
    string RootDirectory { get; }

    string LogsDirectory { get; }

    string ConfigurationDirectory { get; }

    /// <summary>Creates every directory exposed by this instance if it does not already exist.</summary>
    void EnsureCreated();
}
