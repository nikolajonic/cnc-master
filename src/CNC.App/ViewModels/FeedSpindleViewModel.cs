using CNC.App.Mvvm;

namespace CNC.App.ViewModels;

/// <summary>Feed rate override and spindle controls. Spindle commands are enabled in Phase 8.</summary>
public sealed class FeedSpindleViewModel : ObservableObject
{
    private double _feedRate;
    private int _selectedFeedOverride = 100;
    private double _spindleRpmSetpoint = 10000;
    private double _spindleActualRpm;
    private string _spindleDirection = "Stopped";

    public FeedSpindleViewModel()
    {
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
}
