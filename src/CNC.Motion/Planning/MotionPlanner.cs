using System.Globalization;
using CNC.Core.Common;
using CNC.Core.Geometry;
using CNC.Motion.Interpolation;
using CNC.Motion.Limits;

namespace CNC.Motion.Planning;

/// <summary>
/// Turns motion requests into a validated <see cref="MotionPlan"/>: arcs are split into chords,
/// every point is checked against the software travel limits and disabled axes, and each segment
/// gets its speed, acceleration and corner limits. The whole request list is planned at once,
/// so the plan always ends at rest.
/// </summary>
public sealed class MotionPlanner
{
    private const double StraightCosine = -0.999999;
    private const double ReversalCosine = 0.999999;

    private readonly PlannerOptions _options;

    public MotionPlanner(PlannerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public PlannerOptions Options => _options;

    public Result<MotionPlan> Plan(MotionLimits limits, Position start, IEnumerable<MotionRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(requests);

        if (limits.FindTravelViolation(start) is { } startViolation)
        {
            return Result<MotionPlan>.Failure(Invariant(
                $"The current position {start} is outside the {startViolation.Axis} travel limits ({startViolation.MinPosition:0.###} to {startViolation.MaxPosition:0.###})."));
        }

        var drafts = new List<Draft>();
        var current = start;
        foreach (var request in requests)
        {
            ArgumentNullException.ThrowIfNull(request);
            var validation = Validate(request);
            if (validation.IsFailure)
            {
                return Result<MotionPlan>.Failure(validation.Error!);
            }

            IReadOnlyList<Position> points;
            if (request is ArcMotion arc)
            {
                try
                {
                    points = ArcInterpolator.Interpolate(
                        current, arc.Target, arc.Center, arc.Plane, arc.Direction, _options.ArcTolerance, _options.MaxSegmentsPerArc);
                }
                catch (ArgumentException ex)
                {
                    return Result<MotionPlan>.Failure(Prefix(request.SourceLine, ex.Message));
                }
            }
            else
            {
                points = [request.Target];
            }

            foreach (var point in points)
            {
                var pointCheck = CheckPoint(limits, current, point, request.SourceLine);
                if (pointCheck.IsFailure)
                {
                    return Result<MotionPlan>.Failure(pointCheck.Error!);
                }

                var delta = point - current;
                var length = delta.Length;
                if (length < _options.MinimumSegmentLength)
                {
                    continue;
                }

                var direction = delta / length;
                drafts.Add(new Draft(
                    current,
                    point,
                    length,
                    direction,
                    request.Kind,
                    request.Kind == MotionKind.Feed ? request.FeedRate / 60.0 : 0,
                    limits.MaxSpeedAlong(direction),
                    limits.MaxAccelerationAlong(direction),
                    request.SourceLine));
                current = point;
            }
        }

        var segments = new MotionSegment[drafts.Count];
        for (var i = 0; i < drafts.Count; i++)
        {
            var d = drafts[i];
            segments[i] = new MotionSegment
            {
                Index = i,
                Start = d.Start,
                End = d.End,
                Length = d.Length,
                Direction = d.Direction,
                Kind = d.Kind,
                FeedRate = d.FeedRate,
                MaxSpeed = d.MaxSpeed,
                Acceleration = d.Acceleration,
                JunctionSpeed = i + 1 < drafts.Count ? JunctionSpeed(d, drafts[i + 1]) : 0,
                SourceLine = d.SourceLine,
            };
        }

        return Result<MotionPlan>.Success(new MotionPlan(start, segments));
    }

    /// <summary>
    /// Corner speed from the junction-deviation model: the speed at which a circle tangent to both
    /// segments, deviating at most <see cref="PlannerOptions.JunctionDeviation"/> from the corner,
    /// can be followed at the allowed acceleration.
    /// </summary>
    private double JunctionSpeed(Draft current, Draft next)
    {
        var cosTheta = -Dot(current.Direction, next.Direction);
        var speedLimit = Math.Min(current.MaxSpeed, next.MaxSpeed);
        if (cosTheta < StraightCosine)
        {
            return speedLimit;
        }

        if (cosTheta > ReversalCosine || _options.JunctionDeviation <= 0)
        {
            return 0;
        }

        var sinHalfTheta = Math.Sqrt(0.5 * (1 - cosTheta));
        var acceleration = Math.Min(current.Acceleration, next.Acceleration);
        var speed = Math.Sqrt(acceleration * _options.JunctionDeviation * sinHalfTheta / (1 - sinHalfTheta));
        return Math.Min(speed, speedLimit);
    }

    private static Result Validate(MotionRequest request)
    {
        if (!IsFinite(request.Target))
        {
            return Result.Failure(Prefix(request.SourceLine, "The target position is not a finite number."));
        }

        if (request.Kind == MotionKind.Feed && !(request.FeedRate > 0 && double.IsFinite(request.FeedRate)))
        {
            return Result.Failure(Prefix(request.SourceLine, "The feed rate must be greater than zero for a feed move."));
        }

        if (request is ArcMotion arc && !IsFinite(arc.Center))
        {
            return Result.Failure(Prefix(request.SourceLine, "The arc centre is not a finite number."));
        }

        return Result.Success();
    }

    private static Result CheckPoint(MotionLimits limits, Position from, Position to, int sourceLine)
    {
        foreach (var axis in limits.Axes)
        {
            if (!axis.IsEnabled && Math.Abs(to[axis.Axis] - from[axis.Axis]) > MotionLimits.PositionTolerance)
            {
                return Result.Failure(Prefix(sourceLine, $"Axis {axis.Axis} is disabled and cannot move."));
            }
        }

        if (limits.FindTravelViolation(to) is { } violation)
        {
            return Result.Failure(Prefix(sourceLine, Invariant(
                $"Move to {to} exceeds the {violation.Axis} travel limits ({violation.MinPosition:0.###} to {violation.MaxPosition:0.###}).")));
        }

        return Result.Success();
    }

    private static string Prefix(int sourceLine, string message) =>
        sourceLine > 0 ? Invariant($"Line {sourceLine}: {message}") : message;

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static bool IsFinite(Position p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);

    private static double Dot(Position a, Position b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    private readonly record struct Draft(
        Position Start,
        Position End,
        double Length,
        Position Direction,
        MotionKind Kind,
        double FeedRate,
        double MaxSpeed,
        double Acceleration,
        int SourceLine);
}
