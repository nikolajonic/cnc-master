using Microsoft.Extensions.Logging;

namespace CNC.App.Mvvm;

/// <summary>
/// Command for asynchronous operations. While an execution is in flight the command reports
/// <see cref="CanExecute"/> as <c>false</c>, so a second click cannot start the same operation twice.
/// Exceptions are logged and forwarded to an optional handler instead of escaping to the dispatcher.
/// </summary>
public sealed class AsyncRelayCommand : IRaiseCanExecuteChanged
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly ILogger _logger;
    private readonly Action<Exception>? _onException;
    private CancellationTokenSource? _cancellation;

    public AsyncRelayCommand(
        Func<CancellationToken, Task> execute,
        ILogger logger,
        Func<bool>? canExecute = null,
        Action<Exception>? onException = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _canExecute = canExecute;
        _onException = onException;
    }

    public event EventHandler? CanExecuteChanged;

    public bool IsRunning => _cancellation is not null;

    public bool CanExecute(object? parameter) => !IsRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter) => await ExecuteAsync().ConfigureAwait(true);

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        RaiseCanExecuteChanged();

        try
        {
            await _execute(cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _logger.LogDebug("Command execution cancelled");
        }
#pragma warning disable CA1031 // Commands are UI entry points; failures must be reported, not crash the dispatcher.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Command execution failed");
            _onException?.Invoke(ex);
        }
        finally
        {
            _cancellation = null;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>Requests cancellation of the running execution, if any.</summary>
    public void Cancel() => _cancellation?.Cancel();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
