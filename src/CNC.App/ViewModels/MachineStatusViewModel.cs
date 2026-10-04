using CNC.App.Mvvm;

namespace CNC.App.ViewModels;

/// <summary>
/// Top status strip. Values are placeholders until the machine state model (Phase 2) and the
/// controller abstraction are connected.
/// </summary>
public sealed class MachineStatusViewModel : ObservableObject
{
    private string _machineState = "Disconnected";
    private bool _isConnected;
    private string _controllerMode = "Simulation";
    private bool _isEmergencyStopActive;
    private double _feedRate;
    private double _spindleRpm;
    private string _activeLine = "-";
    private string _programName = "(none)";
    private string _controllerStatus = "No controller";

    public string MachineState
    {
        get => _machineState;
        set => SetProperty(ref _machineState, value);
    }

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

    public bool IsEmergencyStopActive
    {
        get => _isEmergencyStopActive;
        set => SetProperty(ref _isEmergencyStopActive, value);
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
}
