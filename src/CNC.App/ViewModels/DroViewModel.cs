using CNC.App.Mvvm;
using CNC.App.Services;
using CNC.Core.Configuration;
using CNC.Core.Units;

namespace CNC.App.ViewModels;

/// <summary>Digital readout panel. Positions become live once the motion controller is connected (Phase 5).</summary>
public sealed class DroViewModel : ObservableObject
{
    private const int DefaultDecimalPlaces = 4;

    private readonly IDispatcherService _dispatcher;
    private bool _isRelative;
    private string _units;

    public DroViewModel(IMachineConfigurationService configuration, IDispatcherService dispatcher)
    {
        _dispatcher = dispatcher;
        _units = configuration.Current.Units.LengthAbbreviation();
        configuration.Changed += OnConfigurationChanged;

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

    public string DistanceModeText => IsRelative ? "Relative" : "Absolute";

    public string WorkCoordinateSystem { get; } = "G54";

    public string Units
    {
        get => _units;
        private set => SetProperty(ref _units, value);
    }

    public IRaiseCanExecuteChanged ZeroAxisCommand { get; }

    public IRaiseCanExecuteChanged ZeroAllCommand { get; }

    private void OnConfigurationChanged(object? sender, MachineConfigurationChangedEventArgs e)
    {
        _ = _dispatcher.InvokeAsync(() => Units = e.Current.Units.LengthAbbreviation());
    }
}
