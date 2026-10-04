using CNC.App.Mvvm;
using CNC.App.Services;
using CNC.Infrastructure.Paths;

namespace CNC.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IAppPaths _paths;
    private string _statusMessage = "Ready. Simulation mode - no controller connected.";

    public MainWindowViewModel(
        MachineStatusViewModel status,
        DroViewModel dro,
        JogViewModel jog,
        FeedSpindleViewModel feedSpindle,
        ProgramControlViewModel program,
        GCodeViewerViewModel gcode,
        ToolpathViewModel toolpath,
        MachineControlsViewModel machineControls,
        IDialogService dialogs,
        IShellService shell,
        IAppPaths paths)
    {
        Status = status;
        Dro = dro;
        Jog = jog;
        FeedSpindle = feedSpindle;
        Program = program;
        GCode = gcode;
        Toolpath = toolpath;
        MachineControls = machineControls;
        _dialogs = dialogs;
        _paths = paths;

        ExitCommand = new RelayCommand(shell.RequestShutdown);
        AboutCommand = new RelayCommand(ShowAbout);
    }

    public string Title { get; } = "CNC Control";

    public MachineStatusViewModel Status { get; }

    public DroViewModel Dro { get; }

    public JogViewModel Jog { get; }

    public FeedSpindleViewModel FeedSpindle { get; }

    public ProgramControlViewModel Program { get; }

    public GCodeViewerViewModel GCode { get; }

    public ToolpathViewModel Toolpath { get; }

    public MachineControlsViewModel MachineControls { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string LogsDirectory => _paths.LogsDirectory;

    public IRaiseCanExecuteChanged ExitCommand { get; }

    public IRaiseCanExecuteChanged AboutCommand { get; }

    private void ShowAbout()
    {
        var version = typeof(MainWindowViewModel).Assembly.GetName().Version;
        _dialogs.ShowInformation(
            $"CNC Control {version}\n\n" +
            "Software controls are not a substitute for machine safety.\n" +
            "The machine must have independent, hardwired emergency stop and limit circuits.\n\n" +
            $"Logs: {_paths.LogsDirectory}",
            "About CNC Control");
    }
}
