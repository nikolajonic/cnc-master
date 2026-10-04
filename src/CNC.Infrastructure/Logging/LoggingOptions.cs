using Serilog.Events;

namespace CNC.Infrastructure.Logging;

public sealed record LoggingOptions
{
    public const string LogFilePrefix = "cnc-";

    public LogEventLevel MinimumLevel { get; init; } = LogEventLevel.Information;

    public int RetainedFileCount { get; init; } = 14;

    public long FileSizeLimitBytes { get; init; } = 10 * 1024 * 1024;

    public bool WriteToDebug { get; init; } = true;
}
