using CNC.App.Mvvm;
using Microsoft.Extensions.Logging;

namespace CNC.App.ViewModels;

/// <summary>
/// Cycle start, feed hold, stop, reset and emergency stop. Emergency stop is always enabled and
/// never gated by machine state; it will route to the controller once one exists (Phase 8).
/// </summary>
public sealed class MachineControlsViewModel : ObservableObject
{
    private readonly ILogger<MachineControlsViewModel> _logger;

    public MachineControlsViewModel(ILogger<MachineControlsViewModel> logger)
    {
        _logger = logger;

        CycleStartCommand = new RelayCommand(static () => { }, static () => false);
        FeedHoldCommand = new RelayCommand(static () => { }, static () => false);
        StopCommand = new RelayCommand(static () => { }, static () => false);
        ResetCommand = new RelayCommand(static () => { }, static () => false);
        EmergencyStopCommand = new RelayCommand(OnEmergencyStop);
    }

    public IRaiseCanExecuteChanged CycleStartCommand { get; }

    public IRaiseCanExecuteChanged FeedHoldCommand { get; }

    public IRaiseCanExecuteChanged StopCommand { get; }

    public IRaiseCanExecuteChanged ResetCommand { get; }

    public IRaiseCanExecuteChanged EmergencyStopCommand { get; }

    private void OnEmergencyStop()
    {
        _logger.LogWarning("Emergency stop requested by operator (no controller connected)");
    }
}
