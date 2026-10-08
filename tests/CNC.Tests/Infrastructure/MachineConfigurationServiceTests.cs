using CNC.Core.Configuration;
using CNC.Infrastructure.Configuration;
using CNC.Infrastructure.Paths;
using CNC.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace CNC.Tests.Infrastructure;

public sealed class MachineConfigurationServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly JsonMachineConfigurationStore _store;
    private readonly MachineConfigurationService _service;

    public MachineConfigurationServiceTests()
    {
        _store = new JsonMachineConfigurationStore(new AppPaths(_temp.Path), new FakeClock(), NullLogger<JsonMachineConfigurationStore>.Instance);
        _service = new MachineConfigurationService(_store, NullLogger<MachineConfigurationService>.Instance);
    }

    public void Dispose()
    {
        _service.Dispose();
        _store.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task Load_SetsCurrentAndRaisesChanged()
    {
        await _store.SaveAsync(MachineConfiguration.CreateDefault() with { Name = "Stored" });
        MachineConfigurationChangedEventArgs? raised = null;
        _service.Changed += (_, e) => raised = e;

        var result = await _service.LoadAsync();

        Assert.Equal("Stored", _service.Current.Name);
        Assert.Same(result, _service.LastLoadResult);
        Assert.NotNull(raised);
        Assert.Equal("Stored", raised.Current.Name);
    }

    [Fact]
    public async Task Update_PersistsAndActivates()
    {
        await _service.LoadAsync();
        var updated = _service.Current with { Name = "Updated" };

        var result = await _service.UpdateAsync(updated);

        Assert.True(result.IsSuccess);
        Assert.Same(updated, _service.Current);
        Assert.Equal("Updated", (await _store.LoadAsync()).Configuration.Name);
    }

    [Fact]
    public async Task Update_Invalid_LeavesCurrentUnchangedAndDoesNotRaise()
    {
        await _service.LoadAsync();
        var before = _service.Current;
        var raised = false;
        _service.Changed += (_, _) => raised = true;

        var result = await _service.UpdateAsync(before with { Name = "" });

        Assert.True(result.IsFailure);
        Assert.Same(before, _service.Current);
        Assert.False(raised);
    }

    [Fact]
    public void BeforeLoad_CurrentIsDefaultAndLastLoadResultIsNull()
    {
        Assert.Equal(MachineConfiguration.CreateDefault().Name, _service.Current.Name);
        Assert.Null(_service.LastLoadResult);
    }
}
