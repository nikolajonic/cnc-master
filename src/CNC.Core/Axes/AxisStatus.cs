namespace CNC.Core.Axes;

/// <summary>Runtime state of one axis as reported by the motion controller.</summary>
public readonly record struct AxisStatus(
    Axis Axis,
    double MachinePosition,
    bool IsEnabled,
    bool IsHomed,
    bool MinLimitTriggered,
    bool MaxLimitTriggered,
    bool HomeSwitchTriggered)
{
    public bool AnyLimitTriggered => MinLimitTriggered || MaxLimitTriggered;

    public static AxisStatus Initial(Axis axis, bool isEnabled = true) =>
        new(axis, 0, isEnabled, IsHomed: false, MinLimitTriggered: false, MaxLimitTriggered: false, HomeSwitchTriggered: false);
}
