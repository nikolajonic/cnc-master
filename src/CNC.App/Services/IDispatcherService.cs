namespace CNC.App.Services;

/// <summary>
/// Marshals work onto the UI thread. Controller and simulation events arrive on background
/// threads and must go through this service before touching bound view model state.
/// </summary>
public interface IDispatcherService
{
    bool CheckAccess();

    void Invoke(Action action);

    T Invoke<T>(Func<T> func);

    Task InvokeAsync(Action action);
}
