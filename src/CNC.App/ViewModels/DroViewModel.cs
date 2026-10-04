using CNC.App.Mvvm;

namespace CNC.App.ViewModels;

/// <summary>Digital readout panel. Positions become live once the motion controller is connected (Phase 5).</summary>
public sealed class DroViewModel : ObservableObject
{
    private const int DefaultDecimalPlaces = 4;

    private bool _isRelative;

    public DroViewModel()
    {
        Axes =
        [
            new AxisReadoutViewModel("X", DefaultDecimalPlaces),
            new AxisReadoutViewModel("Y", DefaultDecimalPlaces),
            new AxisReadoutViewModel("Z", DefaultDecimalPlaces),
        ];

        ZeroAxisCommand = new RelayCommand<string>(static _ => { }, static _ => false);
        ZeroAllCommand = new RelayCommand(static () => { }, static () => false);
    }

    public IReadOnlyList<AxisReadoutViewModel> Axes { get; }

    /// <summary>Absolute (G90) or relative (G91) display mode.</summary>
    public bool IsRelative
    {
        get => _isRelative;
        set
        {
            if (SetProperty(ref _isRelative, value))
            {
                OnPropertyChanged(nameof(DistanceModeText));
            }
        }
    }

    public string DistanceModeText => IsRelative ? "REL" : "ABS";

    public string WorkCoordinateSystem { get; } = "G54";

    public string Units { get; } = "mm";

    public IRaiseCanExecuteChanged ZeroAxisCommand { get; }

    public IRaiseCanExecuteChanged ZeroAllCommand { get; }
}
