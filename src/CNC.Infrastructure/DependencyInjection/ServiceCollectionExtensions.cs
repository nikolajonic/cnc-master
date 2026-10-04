using CNC.Core.Abstractions;
using CNC.Infrastructure.Paths;
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

        return services;
    }
}
