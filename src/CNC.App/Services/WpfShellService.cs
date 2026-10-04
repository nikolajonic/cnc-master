using System.Windows;

namespace CNC.App.Services;

public sealed class WpfShellService : IShellService
{
    private readonly IDispatcherService _dispatcher;

    public WpfShellService(IDispatcherService dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void RequestShutdown() => _dispatcher.Invoke(() => Application.Current?.MainWindow?.Close());
}
