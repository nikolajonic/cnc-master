namespace CNC.App.Services;

/// <summary>Modal user interaction, kept behind an interface so view models stay free of WPF types.</summary>
public interface IDialogService
{
    void ShowInformation(string message, string title);

    void ShowWarning(string message, string title);

    void ShowError(string message, string title);

    bool Confirm(string message, string title);
}
