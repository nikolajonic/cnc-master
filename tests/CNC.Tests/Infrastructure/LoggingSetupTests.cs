using CNC.Infrastructure.Logging;
using CNC.Infrastructure.Paths;
using CNC.Tests.TestSupport;
using Serilog.Core;
using Serilog.Events;

namespace CNC.Tests.Infrastructure;

public sealed class LoggingSetupTests
{
    private static readonly LoggingOptions TestOptions = new() { WriteToDebug = false };

    [Fact]
    public void CreateLogger_CreatesLogsDirectory()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(Path.Combine(temp.Path, "app"));

        using var logger = LoggingSetup.CreateLogger(paths, TestOptions, new LoggingLevelSwitch());

        Assert.True(Directory.Exists(paths.LogsDirectory));
    }

    [Fact]
    public void CreateLogger_WritesEventsToRollingFile()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);

        using (var logger = LoggingSetup.CreateLogger(paths, TestOptions, new LoggingLevelSwitch()))
        {
            logger.Information("Machine state changed to {State}", "Idle");
        }

        var file = Assert.Single(Directory.GetFiles(paths.LogsDirectory, LoggingOptions.LogFilePrefix + "*.log"));
        var content = File.ReadAllText(file);
        Assert.Contains("Machine state changed to Idle", content, StringComparison.Ordinal);
        Assert.Contains("[INF]", content, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateLogger_AppliesConfiguredMinimumLevel()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        var levelSwitch = new LoggingLevelSwitch();

        using var logger = LoggingSetup.CreateLogger(
            paths,
            TestOptions with { MinimumLevel = LogEventLevel.Warning },
            levelSwitch);

        Assert.Equal(LogEventLevel.Warning, levelSwitch.MinimumLevel);
        Assert.False(logger.IsEnabled(LogEventLevel.Information));
        Assert.True(logger.IsEnabled(LogEventLevel.Warning));
    }

    [Fact]
    public void LevelSwitch_ChangesMinimumLevelAtRuntime()
    {
        using var temp = new TempDirectory();
        var levelSwitch = new LoggingLevelSwitch();

        using var logger = LoggingSetup.CreateLogger(new AppPaths(temp.Path), TestOptions, levelSwitch);
        Assert.False(logger.IsEnabled(LogEventLevel.Debug));

        levelSwitch.MinimumLevel = LogEventLevel.Debug;

        Assert.True(logger.IsEnabled(LogEventLevel.Debug));
    }

    [Fact]
    public void DefaultOptions_MatchRetentionPolicy()
    {
        var options = new LoggingOptions();

        Assert.Equal(LogEventLevel.Information, options.MinimumLevel);
        Assert.Equal(14, options.RetainedFileCount);
        Assert.Equal(10 * 1024 * 1024, options.FileSizeLimitBytes);
    }
}
