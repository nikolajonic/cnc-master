using System.Globalization;
using CNC.App.Mvvm;

namespace CNC.App.ViewModels;

/// <summary>One DRO row: axis letter with machine and work positions formatted to a fixed precision.</summary>
public sealed class AxisReadoutViewModel : ObservableObject
{
    private double _machinePosition;
    private double _workPosition;
    private int _decimalPlaces;

    public AxisReadoutViewModel(string axisName, int decimalPlaces)
    {
        AxisName = axisName;
        _decimalPlaces = decimalPlaces;
    }

    public string AxisName { get; }

    public double MachinePosition
    {
        get => _machinePosition;
        set
        {
            if (SetProperty(ref _machinePosition, value))
            {
                OnPropertyChanged(nameof(MachinePositionText));
            }
        }
    }

    public double WorkPosition
    {
        get => _workPosition;
        set
        {
            if (SetProperty(ref _workPosition, value))
            {
                OnPropertyChanged(nameof(WorkPositionText));
            }
        }
    }

    public int DecimalPlaces
    {
        get => _decimalPlaces;
        set
        {
            if (SetProperty(ref _decimalPlaces, Math.Clamp(value, 0, 6)))
            {
                OnPropertyChanged(nameof(MachinePositionText));
                OnPropertyChanged(nameof(WorkPositionText));
            }
        }
    }

    // Invariant culture: machinists expect '.' as the decimal separator regardless of OS locale.
    public string MachinePositionText => Format(_machinePosition);

    public string WorkPositionText => Format(_workPosition);

    private string Format(double value) =>
        value.ToString("+0." + new string('0', _decimalPlaces) + ";-0." + new string('0', _decimalPlaces), CultureInfo.InvariantCulture);
}
