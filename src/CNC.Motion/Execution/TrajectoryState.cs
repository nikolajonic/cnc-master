namespace CNC.Motion.Execution;

public enum TrajectoryState
{
    /// <summary>Following the plan.</summary>
    Running,

    /// <summary>Feed hold requested; decelerating along the path.</summary>
    Holding,

    /// <summary>Feed hold complete; at rest on the path, can resume.</summary>
    Held,

    /// <summary>Stop requested; decelerating along the path, will not resume.</summary>
    Stopping,

    /// <summary>Reached the end of the plan.</summary>
    Completed,

    /// <summary>Ended before the end of the plan by a stop or an abort.</summary>
    Stopped,
}
