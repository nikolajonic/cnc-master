using CNC.App.Mvvm;

namespace CNC.App.ViewModels;

/// <summary>Toolpath viewer. Rendering is implemented in Phase 7; Phase 1 only offers the view selector.</summary>
public sealed class ToolpathViewModel : ObservableObject
{
    private string _selectedPlane = "XY";

    public IReadOnlyList<string> Planes { get; } = ["XY", "XZ", "YZ"];

    public string SelectedPlane
    {
        get => _selectedPlane;
        set => SetProperty(ref _selectedPlane, value);
    }
}
