using CNC.App.Mvvm;
using CNC.App.Services;
using CNC.Core.Configuration;
using CNC.Core.Units;

namespace CNC.App.ViewModels;

/// <summary>Feed rate override and spindle controls. Spindle commands are enabled in Phase 8.</summary>
public sealed class FeedSpindleViewModel : ObservableObject
{
    private readonly IDispatcherService _dispatcher;
    private string _feedUnits;
    private double _feedRate;
    private int _selectedFeedOverride = 100;
    private double _spindleRpmSetpoint = 10000;
    private double _spindleActualRpm;
    private string _spindleDirection = "Stopped";

    public FeedSpindleViewModel(IMachineConfigurationService configuration, IDispatcherService dispatcher)
    {
        _dispatcher = dispatcher;
        _feedUnits = configuration.Current.Units.FeedRateAbbreviation();
        configuration.Changed += OnConfigurationChanged;

        SpindleStartCommand = new RelayCommand(static () => { }, static () => false);
        SpindleStopCommand = new RelayCommand(static () => { }, static () => false);
        SpindleForwardCommand = new RelayCommand(static () => { }, static () => false);
        SpindleReverseCommand = new RelayCommand(static () => { }, static () => false);
    }

    public double FeedRate
    {
        get => _feedRate;
        set => SetProperty(ref _feedRate, value);
    }

    public IReadOnlyList<int> FeedOverrideOptions { get; } = [0, 25, 50, 75, 100, 110, 120, 150];

    public int SelectedFeedOverride
    {
        get => _selectedFeedOverride;
        set => SetProperty(ref _selectedFeedOverride, value);
    }

    public double SpindleRpmSetpoint
    {
        get => _spindleRpmSetpoint;
        set => SetProperty(ref _spindleRpmSetpoint, Math.Max(0, value));
    }

    public double SpindleActualRpm
    {
        get => _spindleActualRpm;
        set => SetProperty(ref _spindleActualRpm, value);
    }

    public string SpindleDirection
    {
        get => _spindleDirection;
        set => SetProperty(ref _spindleDirection, value);
    }

    public IRaiseCanExecuteChanged SpindleStartCommand { get; }

    public IRaiseCanExecuteChanged SpindleStopCommand { get; }

    public IRaiseCanExecuteChanged SpindleForwardCommand { get; }

    public IRaiseCanExecuteChanged SpindleReverseCommand { get; }

    public string FeedUnits
    {
        get => _feedUnits;
        private set => SetProperty(ref _feedUnits, value);
    }

    private void OnConfigurationChanged(object? sender, MachineConfigurationChangedEventArgs e)
    {
        _ = _dispatcher.InvokeAsync(() => FeedUnits = e.Current.Units.FeedRateAbbreviation());
    }
}
