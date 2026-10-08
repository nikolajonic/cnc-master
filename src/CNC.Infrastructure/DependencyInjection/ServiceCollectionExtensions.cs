using CNC.Core.Abstractions;
using CNC.Core.Configuration;
using CNC.Core.Machine;
using CNC.Hardware.Motion;
using CNC.Infrastructure.Configuration;
using CNC.Infrastructure.Logging;
using CNC.Infrastructure.Paths;
using CNC.Motion.Planning;
using CNC.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Core;

namespace CNC.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers infrastructure services. The paths and level switch are created before the host
    /// so that logging is available during startup; they are shared through the container afterwards.
    /// </summary>
    public static IServiceCollection AddCncInfrastructure(
        this IServiceCollection services,
        IAppPaths paths,
        LoggingLevelSwitch levelSwitch)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(levelSwitch);

        services.TryAddSingleton(paths);
        services.TryAddSingleton(levelSwitch);
        services.TryAddSingleton<IClock, SystemClock>();

        services.TryAddSingleton<IMachineStateMachine>(_ => new MachineStateMachine());
        services.TryAddSingleton<IMachineConfigurationStore, JsonMachineConfigurationStore>();
        services.TryAddSingleton<IMachineConfigurationService, MachineConfigurationService>();

        services.TryAddSingleton(new PlannerOptions());
        services.TryAddSingleton<MotionPlanner>();

        services.TryAddSingleton(new VirtualMotionControllerOptions());
        services.TryAddSingleton<IMotionController, VirtualMotionController>();

        services.AddHostedService<MachineEventLoggingService>();

        return services;
    }
}
