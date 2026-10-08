namespace CNC.Simulation;

public sealed record VirtualMotionControllerOptions
{
    /// <summary>
    /// When true, connecting starts a background loop that advances the simulation in real time.
    /// Tests turn this off and call <see cref="VirtualMotionController.Step"/> instead.
    /// </summary>
    public bool RunRealTimeLoop { get; init; } = true;

    public TimeSpan TickInterval { get; init; } = TimeSpan.FromMilliseconds(10);

    /// <summary>Longest simulated step per tick; a stalled loop resumes smoothly instead of jumping.</summary>
    public TimeSpan MaxTickDuration { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Minimum simulated time between periodic status updates while moving.</summary>
    public TimeSpan StatusInterval { get; init; } = TimeSpan.FromMilliseconds(33);
}
