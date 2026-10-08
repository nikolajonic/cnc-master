using CNC.Core.Common;
using CNC.Core.Configuration;

namespace CNC.Tests.TestSupport;

internal sealed class FakeMachineConfigurationService(MachineConfiguration configuration) : IMachineConfigurationService
{
    public MachineConfiguration Current { get; private set; } = configuration;

    public ConfigurationLoadResult? LastLoadResult => null;

    public event EventHandler<MachineConfigurationChangedEventArgs>? Changed;

    public Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result> UpdateAsync(MachineConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var previous = Current;
        Current = configuration;
        Changed?.Invoke(this, new MachineConfigurationChangedEventArgs(previous, configuration));
        return Task.FromResult(Result.Success());
    }
}
