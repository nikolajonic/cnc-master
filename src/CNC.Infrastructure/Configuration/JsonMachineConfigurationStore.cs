using System.Globalization;
using System.Text.Json;
using CNC.Core.Abstractions;
using CNC.Core.Common;
using CNC.Core.Configuration;
using CNC.Infrastructure.Paths;
using CNC.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace CNC.Infrastructure.Configuration;

public sealed class JsonMachineConfigurationStore : IMachineConfigurationStore, IDisposable
{
    public const string FileName = "machine.json";

    private readonly ILogger<JsonMachineConfigurationStore> _logger;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public JsonMachineConfigurationStore(IAppPaths paths, IClock clock, ILogger<JsonMachineConfigurationStore> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        FilePath = Path.Combine(paths.ConfigurationDirectory, FileName);
    }

    public string FilePath { get; }

    public async Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
            {
                var defaults = MachineConfiguration.CreateDefault();
                await WriteAsync(defaults, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("No machine configuration found; created defaults at {Path}", FilePath);
                return new ConfigurationLoadResult(defaults, ConfigurationLoadStatus.CreatedDefault,
                    $"No machine configuration was found. Defaults were written to {FilePath}.");
            }

            var problem = await TryReadAsync(cancellationToken).ConfigureAwait(false);
            if (problem.Configuration is { } configuration)
            {
                _logger.LogInformation("Loaded machine configuration '{Name}' from {Path}", configuration.Name, FilePath);
                return new ConfigurationLoadResult(configuration, ConfigurationLoadStatus.Loaded);
            }

            return await RecoverAsync(problem.Error!, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<Result> SaveAsync(MachineConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var errors = MachineConfigurationValidator.Validate(configuration);
        if (errors.Count > 0)
        {
            var message = "The machine configuration is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, errors);
            _logger.LogWarning("Rejected invalid machine configuration: {Errors}", string.Join("; ", errors));
            return Result.Failure(message);
        }

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteAsync(configuration, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Saved machine configuration '{Name}' to {Path}", configuration.Name, FilePath);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to save machine configuration to {Path}", FilePath);
            return Result.Failure($"Could not save the machine configuration to {FilePath}: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void Dispose() => _fileLock.Dispose();

    private async Task<(MachineConfiguration? Configuration, string? Error)> TryReadAsync(CancellationToken cancellationToken)
    {
        MachineConfiguration? configuration;
        try
        {
            await using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            configuration = await JsonSerializer
                .DeserializeAsync<MachineConfiguration>(stream, JsonDefaults.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            var location = ex.LineNumber is { } line ? $" (line {line + 1})" : string.Empty;
            return (null, $"The file is not valid JSON{location}: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, $"The file could not be read: {ex.Message}");
        }

        if (configuration is null)
        {
            return (null, "The file is empty.");
        }

        var errors = MachineConfigurationValidator.Validate(configuration);
        if (errors.Count > 0)
        {
            return (null, "The file contains invalid values:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        return (configuration, null);
    }

    private async Task<ConfigurationLoadResult> RecoverAsync(string error, CancellationToken cancellationToken)
    {
        var timestamp = _clock.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(
            Path.GetDirectoryName(FilePath)!,
            $"{Path.GetFileNameWithoutExtension(FilePath)}.invalid-{timestamp}.json");

        File.Copy(FilePath, backupPath, overwrite: true);

        var defaults = MachineConfiguration.CreateDefault();
        await WriteAsync(defaults, cancellationToken).ConfigureAwait(false);

        _logger.LogError(
            "Machine configuration at {Path} could not be used and was backed up to {BackupPath}. Defaults loaded. {Error}",
            FilePath, backupPath, error);

        return new ConfigurationLoadResult(
            defaults,
            ConfigurationLoadStatus.RecoveredWithDefaults,
            $"The machine configuration could not be used and default settings were loaded.{Environment.NewLine}{Environment.NewLine}" +
            $"{error}{Environment.NewLine}{Environment.NewLine}The original file was saved as:{Environment.NewLine}{backupPath}");
    }

    private Task WriteAsync(MachineConfiguration configuration, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(configuration, JsonDefaults.Options);
        return AtomicFile.WriteAllTextAsync(FilePath, json, cancellationToken);
    }
}
