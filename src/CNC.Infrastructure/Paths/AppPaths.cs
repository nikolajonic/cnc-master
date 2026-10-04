namespace CNC.Infrastructure.Paths;

public sealed class AppPaths : IAppPaths
{
    public const string ApplicationFolderName = "CncControl";

    public AppPaths(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        RootDirectory = Path.GetFullPath(rootDirectory);
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        ConfigurationDirectory = Path.Combine(RootDirectory, "config");
    }

    public string RootDirectory { get; }

    public string LogsDirectory { get; }

    public string ConfigurationDirectory { get; }

    /// <summary>
    /// Uses <c>%LocalAppData%\CncControl</c> on Windows and the platform equivalent elsewhere.
    /// </summary>
    public static AppPaths CreateDefault()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);

        if (string.IsNullOrEmpty(localAppData))
        {
            localAppData = AppContext.BaseDirectory;
        }

        return new AppPaths(Path.Combine(localAppData, ApplicationFolderName));
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ConfigurationDirectory);
    }
}
