using CNC.Core.Abstractions;
using CNC.Core.Configuration;
using CNC.Core.Machine;
using CNC.Hardware.Motion;
using CNC.Infrastructure.Configuration;
using CNC.Infrastructure.DependencyInjection;
using CNC.Infrastructure.Logging;
using CNC.Infrastructure.Paths;
using CNC.Motion.Planning;
using CNC.Simulation;
using CNC.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

        using var provider = CreateServices()
            .AddCncInfrastructure(paths, levelSwitch)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.Same(paths, provider.GetRequiredService<IAppPaths>());
        Assert.Same(levelSwitch, provider.GetRequiredService<LoggingLevelSwitch>());
        Assert.IsType<SystemClock>(provider.GetRequiredService<IClock>());
        Assert.IsType<MachineStateMachine>(provider.GetRequiredService<IMachineStateMachine>());
        Assert.IsType<JsonMachineConfigurationStore>(provider.GetRequiredService<IMachineConfigurationStore>());
        Assert.IsType<MachineConfigurationService>(provider.GetRequiredService<IMachineConfigurationService>());
        Assert.Contains(provider.GetServices<IHostedService>(), s => s is MachineEventLoggingService);
        Assert.NotNull(provider.GetRequiredService<MotionPlanner>());
        var controller = Assert.IsType<VirtualMotionController>(provider.GetRequiredService<IMotionController>());
        Assert.Equal(ControllerMode.Simulation, controller.Mode);
        Assert.False(controller.IsConnected);
    }

    [Fact]
    public void AddCncInfrastructure_RegistersSingletons()
    {
        using var temp = new TempDirectory();

        using var provider = CreateServices()
            .AddCncInfrastructure(new AppPaths(temp.Path), new LoggingLevelSwitch())
            .BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IClock>(), provider.GetRequiredService<IClock>());
        Assert.Same(provider.GetRequiredService<IMachineStateMachine>(), provider.GetRequiredService<IMachineStateMachine>());
        Assert.Same(provider.GetRequiredService<IMachineConfigurationService>(), provider.GetRequiredService<IMachineConfigurationService>());
    }

    [Fact]
    public void AddCncInfrastructure_KeepsExistingClockRegistration()
    {
        using var temp = new TempDirectory();
        var testClock = new FakeClock();

        using var provider = CreateServices()
            .AddSingleton<IClock>(testClock)
            .AddCncInfrastructure(new AppPaths(temp.Path), new LoggingLevelSwitch())
            .BuildServiceProvider();

        Assert.Same(testClock, provider.GetRequiredService<IClock>());
    }

    [Fact]
    public void StateMachine_StartsDisconnected()
    {
        using var temp = new TempDirectory();

        using var provider = CreateServices()
            .AddCncInfrastructure(new AppPaths(temp.Path), new LoggingLevelSwitch())
            .BuildServiceProvider();

        Assert.Equal(MachineState.Disconnected, provider.GetRequiredService<IMachineStateMachine>().Current);
    }

    private static IServiceCollection CreateServices() =>
        new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
}
