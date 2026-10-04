using System.Windows.Threading;
using CNC.App.Services;
using CNC.App.ViewModels;
using CNC.Infrastructure.DependencyInjection;
using CNC.Infrastructure.Paths;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;

namespace CNC.App.Composition;

/// <summary>Composition root. The only place where concrete implementations are chosen.</summary>
internal static class AppHostBuilder
{
    public static IHost Build(IAppPaths paths, LoggingLevelSwitch levelSwitch, Dispatcher dispatcher)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: false);

        builder.Services.AddCncInfrastructure(paths, levelSwitch);
        AddUiServices(builder.Services, dispatcher);
        AddViewModels(builder.Services);
        builder.Services.AddSingleton<MainWindow>();

        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        }));

        return builder.Build();
    }

    private static void AddUiServices(IServiceCollection services, Dispatcher dispatcher)
    {
        services.AddSingleton<IDispatcherService>(new WpfDispatcherService(dispatcher));
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<IShellService, WpfShellService>();
    }

    private static void AddViewModels(IServiceCollection services)
    {
        services.AddSingleton<MachineStatusViewModel>();
        services.AddSingleton<DroViewModel>();
        services.AddSingleton<JogViewModel>();
        services.AddSingleton<FeedSpindleViewModel>();
        services.AddSingleton<ProgramControlViewModel>();
        services.AddSingleton<GCodeViewerViewModel>();
        services.AddSingleton<ToolpathViewModel>();
        services.AddSingleton<MachineControlsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
