using System.Globalization;
using CNC.Infrastructure.Paths;
using Serilog;
using Serilog.Core;

namespace CNC.Infrastructure.Logging;

public static class LoggingSetup
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] (T{ThreadId}) {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Builds the application logger. Files roll daily and when they exceed the size limit;
    /// the minimum level can be changed at runtime through <paramref name="levelSwitch"/>.
    /// </summary>
    public static Logger CreateLogger(IAppPaths paths, LoggingOptions options, LoggingLevelSwitch levelSwitch)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(levelSwitch);

        paths.EnsureCreated();
        levelSwitch.MinimumLevel = options.MinimumLevel;

        var configuration = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .Enrich.FromLogContext()
            .Enrich.With<ThreadIdEnricher>()
            .WriteTo.File(
                path: GetLogFilePathPattern(paths),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: options.FileSizeLimitBytes,
                retainedFileCountLimit: options.RetainedFileCount,
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                outputTemplate: OutputTemplate,
                formatProvider: CultureInfo.InvariantCulture);

        if (options.WriteToDebug)
        {
            configuration = configuration.WriteTo.Debug(
                outputTemplate: OutputTemplate,
                formatProvider: CultureInfo.InvariantCulture);
        }

        return configuration.CreateLogger();
    }

    public static string GetLogFilePathPattern(IAppPaths paths) =>
        Path.Combine(paths.LogsDirectory, LoggingOptions.LogFilePrefix + ".log");
}
