using CNC.App.Mvvm;

namespace CNC.App.ViewModels;

/// <summary>Jog controls. Commands stay disabled until a controller is available (Phase 5).</summary>
public sealed class JogViewModel : ObservableObject
{
    private double _selectedStep = 1.0;

    public JogViewModel()
    {
        JogCommand = new RelayCommand<string>(static _ => { }, static _ => false);
    }

    public IReadOnlyList<double> JogSteps { get; } = [0.001, 0.01, 0.1, 1, 10, 100];

    public double SelectedStep
    {
        get => _selectedStep;
        set => SetProperty(ref _selectedStep, value);
    }

    /// <summary>Parameter is an axis and direction such as <c>X+</c> or <c>Z-</c>.</summary>
    public IRaiseCanExecuteChanged JogCommand { get; }
}
