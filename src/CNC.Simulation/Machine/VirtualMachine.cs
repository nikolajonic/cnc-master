using System.Globalization;
using CNC.Core.Common;
using CNC.Core.Geometry;
using CNC.Hardware.Motion;
using CNC.Motion.Execution;
using CNC.Motion.Limits;
using CNC.Motion.Planning;

namespace CNC.Simulation.Machine;

/// <summary>
/// Simulated three-axis machine. Time only moves when <see cref="Advance"/> is called, which keeps
/// the simulation deterministic for tests; <see cref="VirtualMotionController"/> drives it in real time.
/// Thread-safe: commands and time steps may come from different threads.
/// </summary>
public sealed class VirtualMachine
{
    /// <summary>Largest gap tolerated between a plan's start and the current position.</summary>
    private const double StartTolerance = 1e-4;

    private readonly object _sync = new();
    private readonly List<MotionCompletedEventArgs> _completions = [];
    private readonly List<MotionFaultEventArgs> _faults = [];
    private MotionLimits _limits;
    private Position _position;
    private TrajectoryExecutor? _executor;
    private double _feedOverride = 1.0;
    private bool _emergencyStop;
    private MotionFault? _fault;

    public VirtualMachine(MotionLimits limits, Position initialPosition)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _position = initialPosition;
    }

    public Position Position
    {
        get
        {
            lock (_sync)
            {
                return _position;
            }
        }
    }

    public MotionStatus GetStatus()
    {
        lock (_sync)
        {
            return new MotionStatus(
                CurrentState(),
                _position,
                _executor?.Velocity ?? Position.Zero,
                _feedOverride,
                _executor?.SourceLine ?? 0,
                _executor?.SegmentIndex ?? 0,
                _executor?.Plan.Segments.Count ?? 0,
                _executor?.Progress ?? 0);
        }
    }

    public void UpdateLimits(MotionLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        lock (_sync)
        {
            _limits = limits;
        }
    }

    public Result Execute(MotionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (_sync)
        {
            if (_emergencyStop)
            {
                return Result.Failure("Emergency stop is active.");
            }

            if (_fault is not null)
            {
                return Result.Failure($"The machine is in a fault state: {_fault.Message}");
            }

            if (_executor is not null)
            {
                return Result.Failure("Motion is already in progress.");
            }

            if (plan.Start.DistanceTo(_position) > StartTolerance)
            {
                return Result.Failure(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The plan starts at {plan.Start} but the machine is at {_position}."));
            }

            var executor = new TrajectoryExecutor(plan, _feedOverride);
            if (executor.IsFinished)
            {
                _completions.Add(new MotionCompletedEventArgs(MotionCompletion.Completed, 0));
                return Result.Success();
            }

            _executor = executor;
            return Result.Success();
        }
    }

    public Result FeedHold()
    {
        lock (_sync)
        {
            if (_executor is null || _executor.State is not (TrajectoryState.Running or TrajectoryState.Holding or TrajectoryState.Held))
            {
                return Result.Failure("There is no motion to hold.");
            }

            _executor.Hold();
            return Result.Success();
        }
    }

    public Result Resume()
    {
        lock (_sync)
        {
            if (_executor is null || _executor.State is not (TrajectoryState.Holding or TrajectoryState.Held))
            {
                return Result.Failure("Motion is not on feed hold.");
            }

            _executor.Resume();
            return Result.Success();
        }
    }

    public Result Stop()
    {
        lock (_sync)
        {
            if (_executor is null)
            {
                return Result.Success();
            }

            _executor.Stop();
            CollectFinishedExecutor();
            return Result.Success();
        }
    }

    /// <summary>Stops motion instantly. Always succeeds.</summary>
    public void EmergencyStop()
    {
        lock (_sync)
        {
            _emergencyStop = true;
            Abort(MotionCompletion.Aborted);
        }
    }

    /// <summary>Aborts motion without entering emergency stop (used on disconnect).</summary>
    public void AbortMotion()
    {
        lock (_sync)
        {
            Abort(MotionCompletion.Aborted);
        }
    }

    public Result Reset()
    {
        lock (_sync)
        {
            if (_executor is not null)
            {
                return Result.Failure("Stop motion before resetting.");
            }

            _emergencyStop = false;
            _fault = null;
            return Result.Success();
        }
    }

    public Result SetFeedOverride(double factor)
    {
        if (!(factor >= 0 && factor <= TrajectoryExecutor.MaxFeedOverride))
        {
            return Result.Failure(string.Create(
                CultureInfo.InvariantCulture,
                $"Feed override must be between 0 % and {TrajectoryExecutor.MaxFeedOverride * 100:0} %."));
        }

        lock (_sync)
        {
            _feedOverride = factor;
            if (_executor is not null)
            {
                _executor.FeedOverride = factor;
            }

            return Result.Success();
        }
    }

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return;
        }

        lock (_sync)
        {
            if (_executor is null)
            {
                return;
            }

            _executor.Advance(elapsed);
            _position = _executor.Position;

            if (_limits.FindTravelViolation(_position) is { } violation)
            {
                var fault = new MotionFault(
                    MotionFaultKind.SoftLimit,
                    string.Create(CultureInfo.InvariantCulture, $"Axis {violation.Axis} left its travel limits at {_position}."),
                    violation.Axis);
                _fault = fault;
                _faults.Add(new MotionFaultEventArgs(fault));
                Abort(MotionCompletion.Faulted);
                return;
            }

            CollectFinishedExecutor();
        }
    }

    /// <summary>Removes and returns events produced since the last call.</summary>
    public (IReadOnlyList<MotionCompletedEventArgs> Completions, IReadOnlyList<MotionFaultEventArgs> Faults) DrainEvents()
    {
        lock (_sync)
        {
            if (_completions.Count == 0 && _faults.Count == 0)
            {
                return ([], []);
            }

            var result = (_completions.ToArray(), _faults.ToArray());
            _completions.Clear();
            _faults.Clear();
            return result;
        }
    }

    private void Abort(MotionCompletion completion)
    {
        if (_executor is null)
        {
            return;
        }

        _executor.Abort();
        _position = _executor.Position;
        _completions.Add(new MotionCompletedEventArgs(completion, _executor.SourceLine));
        _executor = null;
    }

    private void CollectFinishedExecutor()
    {
        if (_executor is not { IsFinished: true } finished)
        {
            return;
        }

        _position = finished.Position;
        var completion = finished.State == TrajectoryState.Completed ? MotionCompletion.Completed : MotionCompletion.Stopped;
        _completions.Add(new MotionCompletedEventArgs(completion, finished.SourceLine));
        _executor = null;
    }

    private MotionState CurrentState()
    {
        if (_emergencyStop)
        {
            return MotionState.EmergencyStop;
        }

        if (_fault is not null)
        {
            return MotionState.Fault;
        }

        return _executor?.State switch
        {
            null => MotionState.Idle,
            TrajectoryState.Running => MotionState.Running,
            TrajectoryState.Holding => MotionState.Holding,
            TrajectoryState.Held => MotionState.Held,
            TrajectoryState.Stopping => MotionState.Stopping,
            _ => MotionState.Idle,
        };
    }
}
