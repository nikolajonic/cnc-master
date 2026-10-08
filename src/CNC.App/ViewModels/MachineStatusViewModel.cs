using CNC.App.Mvvm;
using CNC.App.Services;
using CNC.Core.Machine;

namespace CNC.App.ViewModels;

/// <summary>
/// Top status strip. Machine state is live; connection, feed, spindle and program values are
/// placeholders until the controller (Phase 4) and execution (Phase 6) are connected.
/// </summary>
public sealed class MachineStatusViewModel : ObservableObject
{
    private readonly IDispatcherService _dispatcher;
    private MachineState _state;
    private bool _isConnected;
    private string _controllerMode = "Simulation";
    private double _feedRate;
    private double _spindleRpm;
    private string _activeLine = "-";
    private string _programName = "(none)";
    private string _controllerStatus = "No controller";

    public MachineStatusViewModel(IMachineStateMachine stateMachine, IDispatcherService dispatcher)
    {
        _dispatcher = dispatcher;
        _state = stateMachine.Current;
        stateMachine.StateChanged += OnStateChanged;
    }

    public MachineState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(IsEmergencyStopActive));
                OnPropertyChanged(nameof(IsAlarmActive));
            }
        }
    }

    public string StateText => State switch
    {
        MachineState.EmergencyStop => "Emergency stop",
        _ => State.ToString(),
    };

    public bool IsEmergencyStopActive => State == MachineState.EmergencyStop;

    public bool IsAlarmActive => State == MachineState.Alarm;

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string ControllerMode
    {
        get => _controllerMode;
        set => SetProperty(ref _controllerMode, value);
    }

    public double FeedRate
    {
        get => _feedRate;
        set => SetProperty(ref _feedRate, value);
    }

    public double SpindleRpm
    {
        get => _spindleRpm;
        set => SetProperty(ref _spindleRpm, value);
    }

    public string ActiveLine
    {
        get => _activeLine;
        set => SetProperty(ref _activeLine, value);
    }

    public string ProgramName
    {
        get => _programName;
        set => SetProperty(ref _programName, value);
    }

    public string ControllerStatus
    {
        get => _controllerStatus;
        set => SetProperty(ref _controllerStatus, value);
    }

    private void OnStateChanged(object? sender, MachineStateChangedEventArgs e)
    {
        _ = _dispatcher.InvokeAsync(() => State = e.Current);
    }
}
