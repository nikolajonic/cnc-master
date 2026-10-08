using CNC.Core.Axes;
using CNC.Core.Configuration;
using CNC.Core.Units;
using CNC.Infrastructure.Configuration;
using CNC.Infrastructure.Paths;
using CNC.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace CNC.Tests.Infrastructure;

public sealed class JsonMachineConfigurationStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly JsonMachineConfigurationStore _store;

    public JsonMachineConfigurationStoreTests()
    {
        _store = CreateStore();
    }

    public void Dispose()
    {
        _store.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task Load_WhenFileMissing_CreatesDefaults()
    {
        var result = await _store.LoadAsync();

        Assert.Equal(ConfigurationLoadStatus.CreatedDefault, result.Status);
        Assert.Equal(MachineConfiguration.CreateDefault().Name, result.Configuration.Name);
        Assert.True(File.Exists(_store.FilePath));
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsAllValues()
    {
        var original = MachineConfiguration.CreateDefault() with
        {
            Name = "Test Mill",
            Units = MeasurementSystem.Imperial,
            Spindle = new SpindleConfiguration { MinRpm = 100, MaxRpm = 9000, Acceleration = 1500, SupportsReverse = false },
        };
        original = original.WithAxis(original.GetAxis(Axis.Z) with { InvertDirection = true, StepsPerUnit = 1234.5 });

        Assert.True((await _store.SaveAsync(original)).IsSuccess);

        using var reloadStore = CreateStore();
        var result = await reloadStore.LoadAsync();

        Assert.Equal(ConfigurationLoadStatus.Loaded, result.Status);
        var loaded = result.Configuration;
        Assert.Equal("Test Mill", loaded.Name);
        Assert.Equal(MeasurementSystem.Imperial, loaded.Units);
        Assert.Equal(original.Spindle, loaded.Spindle);
        Assert.Equal(original.Axes, loaded.Axes);
    }

    [Fact]
    public async Task Save_WritesEnumsAsNamesInCamelCase()
    {
        await _store.SaveAsync(MachineConfiguration.CreateDefault());

        var json = await File.ReadAllTextAsync(_store.FilePath);

        Assert.Contains("\"units\": \"Metric\"", json, StringComparison.Ordinal);
        Assert.Contains("\"axis\": \"X\"", json, StringComparison.Ordinal);
        Assert.Contains("\"homeDirection\": \"Positive\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_InvalidConfiguration_IsRejectedAndNotWritten()
    {
        var invalid = MachineConfiguration.CreateDefault() with { Name = "" };

        var result = await _store.SaveAsync(invalid);

        Assert.True(result.IsFailure);
        Assert.Contains("name", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(_store.FilePath));
    }

    [Fact]
    public async Task Save_KeepsPreviousVersionAsBackupAndLeavesNoTempFile()
    {
        await _store.SaveAsync(MachineConfiguration.CreateDefault() with { Name = "First" });
        await _store.SaveAsync(MachineConfiguration.CreateDefault() with { Name = "Second" });

        Assert.Contains("First", await File.ReadAllTextAsync(_store.FilePath + ".bak"), StringComparison.Ordinal);
        Assert.Contains("Second", await File.ReadAllTextAsync(_store.FilePath), StringComparison.Ordinal);
        Assert.False(File.Exists(_store.FilePath + ".tmp"));
    }

    [Fact]
    public async Task Load_MalformedJson_BacksUpFileAndRecoversDefaults()
    {
        await WriteConfigFileAsync("{ \"name\": \"Broken\", ");

        var result = await _store.LoadAsync();

        Assert.Equal(ConfigurationLoadStatus.RecoveredWithDefaults, result.Status);
        Assert.Contains("not valid JSON", result.Message, StringComparison.Ordinal);
        var backup = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(_store.FilePath)!, "machine.invalid-*.json"));
        Assert.EndsWith("machine.invalid-20260101-120000.json", backup, StringComparison.Ordinal);
        Assert.Contains("Broken", await File.ReadAllTextAsync(backup), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Load_InvalidValues_RecoversAndExplainsWhy()
    {
        await _store.SaveAsync(MachineConfiguration.CreateDefault());
        var json = (await File.ReadAllTextAsync(_store.FilePath)).Replace("\"stepsPerUnit\": 400", "\"stepsPerUnit\": -1", StringComparison.Ordinal);
        await WriteConfigFileAsync(json);

        var result = await _store.LoadAsync();

        Assert.Equal(ConfigurationLoadStatus.RecoveredWithDefaults, result.Status);
        Assert.Contains("stepsPerUnit", result.Message, StringComparison.Ordinal);
        Assert.Empty(MachineConfigurationValidator.Validate(result.Configuration));
    }

    [Fact]
    public async Task Load_UnknownEnumValue_Recovers()
    {
        await _store.SaveAsync(MachineConfiguration.CreateDefault());
        var json = (await File.ReadAllTextAsync(_store.FilePath)).Replace("\"Metric\"", "\"Furlongs\"", StringComparison.Ordinal);
        await WriteConfigFileAsync(json);

        var result = await _store.LoadAsync();

        Assert.Equal(ConfigurationLoadStatus.RecoveredWithDefaults, result.Status);
    }

    [Fact]
    public async Task Load_ToleratesCommentsAndTrailingCommas()
    {
        await _store.SaveAsync(MachineConfiguration.CreateDefault() with { Name = "Commented" });
        var json = await File.ReadAllTextAsync(_store.FilePath);
        json = "// operator notes\n" + json.Insert(json.LastIndexOf('}'), ",");
        await WriteConfigFileAsync(json);

        var result = await _store.LoadAsync();

        Assert.Equal(ConfigurationLoadStatus.Loaded, result.Status);
        Assert.Equal("Commented", result.Configuration.Name);
    }

    private JsonMachineConfigurationStore CreateStore() =>
        new(new AppPaths(_temp.Path), new FakeClock(), NullLogger<JsonMachineConfigurationStore>.Instance);

    private async Task WriteConfigFileAsync(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_store.FilePath)!);
        await File.WriteAllTextAsync(_store.FilePath, contents);
    }
}
