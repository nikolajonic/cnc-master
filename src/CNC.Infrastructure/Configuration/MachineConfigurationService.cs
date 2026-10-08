using CNC.Core.Common;
using CNC.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace CNC.Infrastructure.Configuration;

public sealed class MachineConfigurationService : IMachineConfigurationService, IDisposable
{
    private readonly IMachineConfigurationStore _store;
    private readonly ILogger<MachineConfigurationService> _logger;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private volatile MachineConfiguration _current = MachineConfiguration.CreateDefault();
    private volatile ConfigurationLoadResult? _lastLoadResult;

    public MachineConfigurationService(IMachineConfigurationStore store, ILogger<MachineConfigurationService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event EventHandler<MachineConfigurationChangedEventArgs>? Changed;

    public MachineConfiguration Current => _current;

    public ConfigurationLoadResult? LastLoadResult => _lastLoadResult;

    public async Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            _lastLoadResult = result;
            Apply(result.Configuration);
            return result;
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public async Task<Result> UpdateAsync(MachineConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        await _updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var saved = await _store.SaveAsync(configuration, cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }

            Apply(configuration);
            _logger.LogInformation("Machine configuration '{Name}' activated", configuration.Name);
            return Result.Success();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public void Dispose() => _updateLock.Dispose();

    private void Apply(MachineConfiguration configuration)
    {
        var previous = _current;
        _current = configuration;
        Changed?.Invoke(this, new MachineConfigurationChangedEventArgs(previous, configuration));
    }
}
