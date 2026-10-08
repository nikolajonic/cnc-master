using System.Text.Json;
using System.Text.Json.Serialization;

namespace CNC.Infrastructure.Persistence;

internal static class JsonDefaults
{
    /// <summary>
    /// Human-editable settings files: indented, camelCase, enums as names, and tolerant of
    /// comments and trailing commas when read.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };
}
