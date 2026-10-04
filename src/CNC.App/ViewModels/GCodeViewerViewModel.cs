using CNC.App.Mvvm;

namespace CNC.App.ViewModels;

/// <summary>
/// G-code viewer. Phase 1 shows plain text; line numbers, highlighting and the active-line
/// marker arrive with the parser and execution phases.
/// </summary>
public sealed class GCodeViewerViewModel : ObservableObject
{
    private string _text = "; No program loaded";

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }
}
