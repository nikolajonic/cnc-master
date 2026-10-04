using System.Windows.Threading;

namespace CNC.App.Services;

public sealed class WpfDispatcherService : IDispatcherService
{
    private readonly Dispatcher _dispatcher;

    public WpfDispatcherService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public bool CheckAccess() => _dispatcher.CheckAccess();

    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }

    public T Invoke<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return _dispatcher.CheckAccess() ? func() : _dispatcher.Invoke(func);
    }

    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _dispatcher.InvokeAsync(action).Task;
    }
}
