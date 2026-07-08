using System.Text.Json;

namespace AqsPluginExtDb.Drivers;

/// <summary>
/// Converts a deserialized request parameter (often a boxed JsonElement) into a plain
/// CLR value suitable for a DbParameter.Value.
/// </summary>
internal static class ParameterConverter
{
    public static object ToProviderValue(object? raw)
    {
        if (raw is null)
        {
            return DBNull.Value;
        }

        if (raw is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? (object)DBNull.Value,
                // Cast to object explicitly: a bare `asLong : element.GetDouble()` ternary would
                // unify both branches to double (long -> double is implicit), silently turning
                // every integral JSON number into a double before it reaches the DbParameter.
                JsonValueKind.Number => element.TryGetInt64(out long asLong) ? (object)asLong : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => DBNull.Value,
                _ => element.GetRawText()
            };
        }

        return raw;
    }
}
