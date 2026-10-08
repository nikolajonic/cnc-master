namespace CNC.Core.Configuration;

/// <param name="Path">Location of the offending value, for example <c>axes[X].maxVelocity</c>.</param>
public sealed record ConfigurationError(string Path, string Message)
{
    public override string ToString() => $"{Path}: {Message}";
}
