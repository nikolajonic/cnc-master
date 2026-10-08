using CNC.App.Mvvm;

namespace CNC.App.ViewModels;

/// <summary>3D toolpath viewer. Rendering, orbit, zoom and pan are implemented in Phase 7.</summary>
public sealed class ToolpathViewModel : ObservableObject
{
    private bool _hasProgram;

    public bool HasProgram
    {
        get => _hasProgram;
        set => SetProperty(ref _hasProgram, value);
    }
}
