namespace CNC.Motion.Profiles;

/// <summary>
/// Constant-acceleration velocity profile over a fixed distance: accelerate from the entry speed,
/// cruise at the peak speed, decelerate to the exit speed. Short moves that cannot reach the
/// maximum speed become triangular (no cruise phase).
/// </summary>
public readonly record struct TrapezoidProfile
{
    private const double FeasibilitySlack = 1e-9;

    private TrapezoidProfile(
        double length,
        double entrySpeed,
        double exitSpeed,
        double peakSpeed,
        double acceleration,
        double accelerationTime,
        double cruiseTime,
        double decelerationTime)
    {
        Length = length;
        EntrySpeed = entrySpeed;
        ExitSpeed = exitSpeed;
        PeakSpeed = peakSpeed;
        Acceleration = acceleration;
        AccelerationTime = accelerationTime;
        CruiseTime = cruiseTime;
        DecelerationTime = decelerationTime;
    }

    public double Length { get; }

    public double EntrySpeed { get; }

    public double ExitSpeed { get; }

    public double PeakSpeed { get; }

    public double Acceleration { get; }

    public double AccelerationTime { get; }

    public double CruiseTime { get; }

    public double DecelerationTime { get; }

    public double Duration => AccelerationTime + CruiseTime + DecelerationTime;

    /// <summary>
    /// Builds a profile. An exit speed that cannot be reached within <paramref name="length"/> is
    /// lowered to the reachable value; an entry speed too high to slow down to the exit speed is an error.
    /// </summary>
    public static TrapezoidProfile Create(double length, double entrySpeed, double exitSpeed, double maxSpeed, double acceleration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfNegative(entrySpeed);
        ArgumentOutOfRangeException.ThrowIfNegative(exitSpeed);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(acceleration);
        if (length > 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSpeed);
        }

        entrySpeed = Math.Min(entrySpeed, maxSpeed);
        exitSpeed = Math.Min(exitSpeed, maxSpeed);

        var reachableExit = Math.Sqrt((entrySpeed * entrySpeed) + (2 * acceleration * length));
        exitSpeed = Math.Min(exitSpeed, reachableExit);

        var maxEntry = Math.Sqrt((exitSpeed * exitSpeed) + (2 * acceleration * length));
        if (entrySpeed > maxEntry * (1 + FeasibilitySlack) + FeasibilitySlack)
        {
            throw new ArgumentException("The entry speed is too high to decelerate to the exit speed within the segment.", nameof(entrySpeed));
        }

        if (length == 0)
        {
            return new TrapezoidProfile(0, entrySpeed, exitSpeed, Math.Max(entrySpeed, exitSpeed), acceleration, 0, 0, 0);
        }

        var peakSquared = ((2 * acceleration * length) + (entrySpeed * entrySpeed) + (exitSpeed * exitSpeed)) / 2;
        if (peakSquared >= maxSpeed * maxSpeed)
        {
            var accelerationDistance = ((maxSpeed * maxSpeed) - (entrySpeed * entrySpeed)) / (2 * acceleration);
            var decelerationDistance = ((maxSpeed * maxSpeed) - (exitSpeed * exitSpeed)) / (2 * acceleration);
            var cruiseDistance = Math.Max(0, length - accelerationDistance - decelerationDistance);
            return new TrapezoidProfile(
                length,
                entrySpeed,
                exitSpeed,
                maxSpeed,
                acceleration,
                (maxSpeed - entrySpeed) / acceleration,
                cruiseDistance / maxSpeed,
                (maxSpeed - exitSpeed) / acceleration);
        }

        var peak = Math.Max(Math.Sqrt(peakSquared), Math.Max(entrySpeed, exitSpeed));
        return new TrapezoidProfile(
            length,
            entrySpeed,
            exitSpeed,
            peak,
            acceleration,
            (peak - entrySpeed) / acceleration,
            0,
            (peak - exitSpeed) / acceleration);
    }

    public double VelocityAt(double time)
    {
        if (time <= 0)
        {
            return EntrySpeed;
        }

        if (time < AccelerationTime)
        {
            return EntrySpeed + (Acceleration * time);
        }

        if (time < AccelerationTime + CruiseTime)
        {
            return PeakSpeed;
        }

        if (time < Duration)
        {
            return PeakSpeed - (Acceleration * (time - AccelerationTime - CruiseTime));
        }

        return ExitSpeed;
    }

    public double DistanceAt(double time)
    {
        if (time <= 0)
        {
            return 0;
        }

        if (time >= Duration)
        {
            return Length;
        }

        if (time < AccelerationTime)
        {
            return (EntrySpeed * time) + (0.5 * Acceleration * time * time);
        }

        var accelerationDistance = (EntrySpeed * AccelerationTime) + (0.5 * Acceleration * AccelerationTime * AccelerationTime);
        if (time < AccelerationTime + CruiseTime)
        {
            return accelerationDistance + (PeakSpeed * (time - AccelerationTime));
        }

        var cruiseDistance = PeakSpeed * CruiseTime;
        var t = time - AccelerationTime - CruiseTime;
        return Math.Min(Length, accelerationDistance + cruiseDistance + (PeakSpeed * t) - (0.5 * Acceleration * t * t));
    }
}
