using CNC.Core.Axes;

namespace CNC.Core.Configuration;

public static class MachineConfigurationValidator
{
    public static IReadOnlyList<ConfigurationError> Validate(MachineConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var errors = new List<ConfigurationError>();

        if (configuration.SchemaVersion is < 1 or > MachineConfiguration.CurrentSchemaVersion)
        {
            errors.Add(new("schemaVersion",
                $"Unsupported schema version {configuration.SchemaVersion}; this application supports version {MachineConfiguration.CurrentSchemaVersion}."));
        }

        if (string.IsNullOrWhiteSpace(configuration.Name))
        {
            errors.Add(new("name", "Machine name is required."));
        }

        if (!Enum.IsDefined(configuration.Units))
        {
            errors.Add(new("units", $"Unknown measurement system '{configuration.Units}'."));
        }

        ValidateAxes(configuration.Axes ?? [], errors);
        ValidateSpindle(configuration.Spindle, errors);

        return errors;
    }

    private static void ValidateAxes(IReadOnlyList<AxisConfiguration> axes, List<ConfigurationError> errors)
    {
        foreach (var axis in LinearAxes.All)
        {
            var count = axes.Count(a => a?.Axis == axis);
            if (count == 0)
            {
                errors.Add(new($"axes[{axis}]", $"Axis {axis} is not configured."));
            }
            else if (count > 1)
            {
                errors.Add(new($"axes[{axis}]", $"Axis {axis} is configured {count} times."));
            }
        }

        foreach (var axis in axes)
        {
            if (axis is null)
            {
                errors.Add(new("axes", "Axis entries must not be null."));
                continue;
            }

            if (!Enum.IsDefined(axis.Axis))
            {
                errors.Add(new("axes", $"Unknown axis '{axis.Axis}'."));
                continue;
            }

            ValidateAxis(axis, errors);
        }
    }

    private static void ValidateAxis(AxisConfiguration axis, List<ConfigurationError> errors)
    {
        var prefix = $"axes[{axis.Axis}]";

        RequirePositive(axis.StepsPerUnit, $"{prefix}.stepsPerUnit", "Steps per unit", errors);
        RequirePositive(axis.MaxVelocity, $"{prefix}.maxVelocity", "Maximum velocity", errors);
        RequirePositive(axis.MaxAcceleration, $"{prefix}.maxAcceleration", "Maximum acceleration", errors);
        RequirePositive(axis.HomingVelocity, $"{prefix}.homingVelocity", "Homing velocity", errors);
        RequireFinite(axis.MinPosition, $"{prefix}.minPosition", "Minimum position", errors);
        RequireFinite(axis.MaxPosition, $"{prefix}.maxPosition", "Maximum position", errors);
        RequireFinite(axis.HomePosition, $"{prefix}.homePosition", "Home position", errors);

        if (double.IsFinite(axis.MinPosition) && double.IsFinite(axis.MaxPosition) && axis.MinPosition >= axis.MaxPosition)
        {
            errors.Add(new($"{prefix}.maxPosition", "Maximum position must be greater than minimum position."));
        }
        else if (double.IsFinite(axis.HomePosition) && (axis.HomePosition < axis.MinPosition || axis.HomePosition > axis.MaxPosition))
        {
            errors.Add(new($"{prefix}.homePosition", "Home position must lie within the travel limits."));
        }

        if (double.IsFinite(axis.HomingVelocity) && double.IsFinite(axis.MaxVelocity) && axis.HomingVelocity > axis.MaxVelocity)
        {
            errors.Add(new($"{prefix}.homingVelocity", "Homing velocity must not exceed maximum velocity."));
        }

        if (!Enum.IsDefined(axis.HomeDirection))
        {
            errors.Add(new($"{prefix}.homeDirection", $"Unknown home direction '{axis.HomeDirection}'."));
        }
    }

    private static void ValidateSpindle(SpindleConfiguration? spindle, List<ConfigurationError> errors)
    {
        if (spindle is null)
        {
            errors.Add(new("spindle", "Spindle configuration is required."));
            return;
        }

        RequireFinite(spindle.MinRpm, "spindle.minRpm", "Minimum RPM", errors);
        RequirePositive(spindle.MaxRpm, "spindle.maxRpm", "Maximum RPM", errors);
        RequirePositive(spindle.Acceleration, "spindle.acceleration", "Spindle acceleration", errors);

        if (spindle.MinRpm < 0)
        {
            errors.Add(new("spindle.minRpm", "Minimum RPM must not be negative."));
        }
        else if (spindle.MinRpm >= spindle.MaxRpm)
        {
            errors.Add(new("spindle.maxRpm", "Maximum RPM must be greater than minimum RPM."));
        }
    }

    private static void RequirePositive(double value, string path, string label, List<ConfigurationError> errors)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            errors.Add(new(path, $"{label} must be a positive number."));
        }
    }

    private static void RequireFinite(double value, string path, string label, List<ConfigurationError> errors)
    {
        if (!double.IsFinite(value))
        {
            errors.Add(new(path, $"{label} must be a finite number."));
        }
    }
}
