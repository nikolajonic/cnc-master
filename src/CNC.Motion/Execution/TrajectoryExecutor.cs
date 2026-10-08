using CNC.Core.Geometry;
using CNC.Motion.Planning;

namespace CNC.Motion.Execution;

/// <summary>
/// Moves a point along a <see cref="MotionPlan"/> over time. Each step accelerates towards the
/// segment's speed cap, never faster than the segment's acceleration, and never above the braking
/// curve that guarantees the next corner and the final stop can be honoured. Feed override,
/// feed hold and controlled stop all reuse the same rule, so they never exceed the acceleration limits.
/// Not thread-safe; the owner serialises access.
/// </summary>
public sealed class TrajectoryExecutor
{
    public const double MaxFeedOverride = 2.0;

    /// <summary>Integration step; small enough that discretisation error is far below a micron at CNC speeds.</summary>
    private const double MaxStepSeconds = 0.001;

    private readonly MotionPlan _plan;
    private readonly IReadOnlyList<MotionSegment> _segments;
    private readonly double[] _exitSpeeds;
    private int _index;
    private double _distanceInSegment;
    private double _completedLength;
    private double _speed;
    private double _feedOverride;

    public TrajectoryExecutor(MotionPlan plan, double feedOverride = 1.0)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ValidateOverride(feedOverride);

        _plan = plan;
        _segments = plan.Segments;
        _exitSpeeds = new double[_segments.Count];
        _feedOverride = feedOverride;
        SpeedLookahead.ComputeExitSpeeds(_segments, _feedOverride, _exitSpeeds);
        State = _segments.Count == 0 ? TrajectoryState.Completed : TrajectoryState.Running;
    }

    public MotionPlan Plan => _plan;

    public TrajectoryState State { get; private set; }

    public bool IsFinished => State is TrajectoryState.Completed or TrajectoryState.Stopped;

    /// <summary>Path speed in units per second.</summary>
    public double Speed => _speed;

    public Position Position => _index < _segments.Count
        ? _segments[_index].PositionAt(_distanceInSegment)
        : _plan.End;

    /// <summary>Velocity vector in units per second.</summary>
    public Position Velocity => _index < _segments.Count ? _segments[_index].Direction * _speed : Position.Zero;

    /// <summary>Index of the segment being executed, or the segment count once finished at the end.</summary>
    public int SegmentIndex => _index;

    /// <summary>Program line of the current segment (or of the last segment once completed).</summary>
    public int SourceLine => _segments.Count == 0
        ? 0
        : _segments[Math.Min(_index, _segments.Count - 1)].SourceLine;

    public double DistanceTravelled => _completedLength + _distanceInSegment;

    /// <summary>Fraction of the path length travelled, 0 to 1.</summary>
    public double Progress => _plan.TotalLength > 0 ? Math.Min(1, DistanceTravelled / _plan.TotalLength) : 1;

    /// <summary>Scale factor for feed moves, 0 to <see cref="MaxFeedOverride"/>. Speed changes respect the acceleration limits.</summary>
    public double FeedOverride
    {
        get => _feedOverride;
        set
        {
            ValidateOverride(value);
            _feedOverride = value;
            if (_index < _segments.Count)
            {
                SpeedLookahead.ComputeExitSpeeds(_segments, _feedOverride, _exitSpeeds, _index);
            }
        }
    }

    /// <summary>Feed hold: decelerate along the path and wait for <see cref="Resume"/>.</summary>
    public void Hold()
    {
        if (State == TrajectoryState.Running)
        {
            State = TrajectoryState.Holding;
        }
    }

    public void Resume()
    {
        if (State is TrajectoryState.Holding or TrajectoryState.Held)
        {
            State = TrajectoryState.Running;
        }
    }

    /// <summary>Controlled stop: decelerate along the path, then end without completing the plan.</summary>
    public void Stop()
    {
        switch (State)
        {
            case TrajectoryState.Running:
            case TrajectoryState.Holding:
                State = TrajectoryState.Stopping;
                break;
            case TrajectoryState.Held:
                State = TrajectoryState.Stopped;
                break;
        }
    }

    /// <summary>Immediate stop without deceleration (emergency stop or fault).</summary>
    public void Abort()
    {
        if (!IsFinished)
        {
            _speed = 0;
            State = TrajectoryState.Stopped;
        }
    }

    public void Advance(TimeSpan elapsed)
    {
        var remaining = elapsed.TotalSeconds;
        while (remaining > 0 && !IsFinished && State != TrajectoryState.Held)
        {
            var step = Math.Min(remaining, MaxStepSeconds);
            remaining -= step;
            Step(step);
        }
    }

    private void Step(double dt)
    {
        while (dt > 0 && _index < _segments.Count)
        {
            var segment = _segments[_index];
            var braking = State is TrajectoryState.Holding or TrajectoryState.Stopping;
            var cap = braking ? 0 : segment.SpeedCap(_feedOverride);
            var remainingInSegment = segment.Length - _distanceInSegment;
            var target = Math.Min(cap, BrakingSpeed(segment.Acceleration, _exitSpeeds[_index], remainingInSegment, dt));

            var next = _speed < target
                ? Math.Min(_speed + (segment.Acceleration * dt), target)
                : Math.Max(_speed - (segment.Acceleration * dt), target);
            var distance = 0.5 * (_speed + next) * dt;

            if (distance >= remainingInSegment)
            {
                var fraction = distance > 0 ? remainingInSegment / distance : 1;
                _speed += (next - _speed) * fraction;
                dt -= dt * fraction;
                _completedLength += segment.Length;
                _distanceInSegment = 0;
                _index++;
                if (_index == _segments.Count)
                {
                    _speed = 0;
                    State = TrajectoryState.Completed;
                    return;
                }
            }
            else
            {
                _distanceInSegment += distance;
                _speed = next;
                dt = 0;
            }

            if (braking && _speed <= 0)
            {
                _speed = 0;
                State = State == TrajectoryState.Holding ? TrajectoryState.Held : TrajectoryState.Stopped;
                return;
            }
        }
    }

    /// <summary>
    /// Highest speed at the end of a step of <paramref name="dt"/> that still lies on or below the
    /// braking curve v² = exit² + 2·a·remaining evaluated at the end of that step. Checking only the
    /// start of the step would let the speed creep above the curve and arrive at corners too fast.
    /// </summary>
    private double BrakingSpeed(double acceleration, double exit, double remaining, double dt)
    {
        var ah = acceleration * dt;
        var discriminant = (ah * ah) + (4 * ((exit * exit) + (2 * acceleration * remaining) - (ah * _speed)));
        return discriminant <= 0 ? 0 : Math.Max(0, 0.5 * (Math.Sqrt(discriminant) - ah));
    }

    private static void ValidateOverride(double value)
    {
        if (!(value >= 0 && value <= MaxFeedOverride))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Feed override must be between 0 and {MaxFeedOverride}.");
        }
    }
}
