using CNC.App.Mvvm;

namespace CNC.App.ViewModels;

/// <summary>Program load/run controls and progress. Execution is wired up in Phase 6.</summary>
public sealed class ProgramControlViewModel : ObservableObject
{
    private string _programName = "(none)";
    private int _currentLine;
    private int _totalLines;
    private TimeSpan _elapsed;
    private TimeSpan? _estimatedTotal;

    public ProgramControlViewModel()
    {
        LoadCommand = new RelayCommand(static () => { }, static () => false);
        RunCommand = new RelayCommand(static () => { }, static () => false);
        PauseCommand = new RelayCommand(static () => { }, static () => false);
        ResumeCommand = new RelayCommand(static () => { }, static () => false);
        StopCommand = new RelayCommand(static () => { }, static () => false);
        ResetCommand = new RelayCommand(static () => { }, static () => false);
    }

    public string ProgramName
    {
        get => _programName;
        set => SetProperty(ref _programName, value);
    }

    public int CurrentLine
    {
        get => _currentLine;
        set
        {
            if (SetProperty(ref _currentLine, value))
            {
                OnPropertyChanged(nameof(ProgressPercent));
                OnPropertyChanged(nameof(LineText));
            }
        }
    }

    public int TotalLines
    {
        get => _totalLines;
        set
        {
            if (SetProperty(ref _totalLines, value))
            {
                OnPropertyChanged(nameof(ProgressPercent));
                OnPropertyChanged(nameof(LineText));
            }
        }
    }

    public double ProgressPercent => TotalLines == 0 ? 0 : 100.0 * CurrentLine / TotalLines;

    public string LineText => $"{CurrentLine} / {TotalLines}";

    public TimeSpan Elapsed
    {
        get => _elapsed;
        set
        {
            if (SetProperty(ref _elapsed, value))
            {
                OnPropertyChanged(nameof(ElapsedText));
            }
        }
    }

    public TimeSpan? EstimatedTotal
    {
        get => _estimatedTotal;
        set
        {
            if (SetProperty(ref _estimatedTotal, value))
            {
                OnPropertyChanged(nameof(EstimatedText));
            }
        }
    }

    public string ElapsedText => FormatDuration(Elapsed);

    public string EstimatedText => EstimatedTotal is { } estimate ? FormatDuration(estimate) : "--:--:--";

    public IRaiseCanExecuteChanged LoadCommand { get; }

    public IRaiseCanExecuteChanged RunCommand { get; }

    public IRaiseCanExecuteChanged PauseCommand { get; }

    public IRaiseCanExecuteChanged ResumeCommand { get; }

    public IRaiseCanExecuteChanged StopCommand { get; }

    public IRaiseCanExecuteChanged ResetCommand { get; }

    private static string FormatDuration(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
}
