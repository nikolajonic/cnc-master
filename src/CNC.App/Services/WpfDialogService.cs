using System.Windows;

namespace CNC.App.Services;

public sealed class WpfDialogService : IDialogService
{
    private readonly IDispatcherService _dispatcher;

    public WpfDialogService(IDispatcherService dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void ShowInformation(string message, string title) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string title) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title) =>
        Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
    {
        return _dispatcher.Invoke(() =>
        {
            var owner = Application.Current?.MainWindow;
            return owner is { IsLoaded: true }
                ? MessageBox.Show(owner, message, title, buttons, image)
                : MessageBox.Show(message, title, buttons, image);
        });
    }
}
