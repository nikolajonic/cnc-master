using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using CNC.App.Composition;
using CNC.App.Services;
using CNC.Core.Configuration;
using CNC.Infrastructure.Logging;
using CNC.Infrastructure.Paths;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;

namespace CNC.App;

[SuppressMessage("Design", "CA1001", Justification = "Disposable fields are released in OnExit, the end of the WPF application lifetime.")]
public partial class App : Application
{
    private const string SingleInstanceMutexName = "CncControl.SingleInstance";
    private static readonly TimeSpan HostStopTimeout = TimeSpan.FromSeconds(5);

    private IHost? _host;
    private Mutex? _singleInstanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A second instance could issue conflicting commands to the same controller.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "CNC Control is already running.",
                "CNC Control",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown(1);
            return;
        }

        var paths = AppPaths.CreateDefault();
        var levelSwitch = new LoggingLevelSwitch();
        Log.Logger = LoggingSetup.CreateLogger(paths, new LoggingOptions(), levelSwitch);
        RegisterGlobalExceptionHandlers();

        Log.Information(
            "Application starting. Version {Version}, OS {OS}, runtime {Runtime}, data folder {DataFolder}",
            typeof(App).Assembly.GetName().Version,
            Environment.OSVersion,
            Environment.Version,
            paths.RootDirectory);

        try
        {
            _host = AppHostBuilder.Build(paths, levelSwitch, Dispatcher);
            await _host.StartAsync().ConfigureAwait(true);
            await LoadMachineConfigurationAsync(_host.Services).ConfigureAwait(true);

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            Log.Information("Application started");
        }
#pragma warning disable CA1031 // Any startup failure must be logged and reported before exiting.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Log.Fatal(ex, "Application failed to start");
            MessageBox.Show(
                $"CNC Control failed to start:\n\n{ex.Message}\n\nSee the log folder for details:\n{paths.LogsDirectory}",
                "CNC Control - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(2);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application shutting down. Exit code {ExitCode}", e.ApplicationExitCode);

        if (_host is not null)
        {
            try
            {
                using var timeout = new CancellationTokenSource(HostStopTimeout);
                _host.StopAsync(timeout.Token).GetAwaiter().GetResult();
            }
#pragma warning disable CA1031 // Shutdown must continue so that logs are flushed.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Log.Error(ex, "Error while stopping the application host");
            }
            finally
            {
                _host.Dispose();
                _host = null;
            }
        }

        Log.Information("Application stopped");
        Log.CloseAndFlush();

        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }

    private static async Task LoadMachineConfigurationAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IMachineConfigurationService>();
        var result = await configuration.LoadAsync().ConfigureAwait(true);

        if (result.Status == ConfigurationLoadStatus.RecoveredWithDefaults)
        {
            services.GetRequiredService<IDialogService>().ShowWarning(
                result.Message ?? "The machine configuration could not be loaded; defaults are in use.",
                "CNC Control - Machine Configuration");
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception on the UI thread");

        // Keep the operator interface alive so the machine can still be stopped from it.
        e.Handled = true;

        var dialogs = _host?.Services.GetService<IDialogService>();
        var message = $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe error has been logged.";
        if (dialogs is not null)
        {
            dialogs.ShowError(message, "CNC Control - Error");
        }
        else
        {
            MessageBox.Show(message, "CNC Control - Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception. Terminating: {IsTerminating}", e.IsTerminating);
        if (e.IsTerminating)
        {
            Log.CloseAndFlush();
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
