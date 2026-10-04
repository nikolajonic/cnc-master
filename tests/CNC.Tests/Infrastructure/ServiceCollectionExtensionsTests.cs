using CNC.Core.Abstractions;
using CNC.Infrastructure.DependencyInjection;
using CNC.Infrastructure.Paths;
using CNC.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace CNC.Tests.Infrastructure;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCncInfrastructure_ResolvesAllServicesWithValidation()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        var levelSwitch = new LoggingLevelSwitch();

        using var provider = new ServiceCollection()
            .AddCncInfrastructure(paths, levelSwitch)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.Same(paths, provider.GetRequiredService<IAppPaths>());
        Assert.Same(levelSwitch, provider.GetRequiredService<LoggingLevelSwitch>());
        Assert.IsType<SystemClock>(provider.GetRequiredService<IClock>());
    }

    [Fact]
    public void AddCncInfrastructure_RegistersClockAsSingleton()
    {
        using var temp = new TempDirectory();

        using var provider = new ServiceCollection()
            .AddCncInfrastructure(new AppPaths(temp.Path), new LoggingLevelSwitch())
            .BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IClock>(), provider.GetRequiredService<IClock>());
    }

    [Fact]
    public void AddCncInfrastructure_KeepsExistingClockRegistration()
    {
        using var temp = new TempDirectory();
        var testClock = new FixedClock();

        using var provider = new ServiceCollection()
            .AddSingleton<IClock>(testClock)
            .AddCncInfrastructure(new AppPaths(temp.Path), new LoggingLevelSwitch())
            .BuildServiceProvider();

        Assert.Same(testClock, provider.GetRequiredService<IClock>());
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public TimeSpan Elapsed => TimeSpan.Zero;
    }
}
