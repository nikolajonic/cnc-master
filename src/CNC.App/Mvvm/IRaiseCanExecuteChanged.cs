using System.Windows.Input;

namespace CNC.App.Mvvm;

/// <summary>
/// Commands in this application re-evaluate <see cref="ICommand.CanExecute"/> only when told to,
/// so that enabled state follows machine state changes explicitly rather than WPF's input heuristics.
/// </summary>
public interface IRaiseCanExecuteChanged : ICommand
{
    void RaiseCanExecuteChanged();
}
