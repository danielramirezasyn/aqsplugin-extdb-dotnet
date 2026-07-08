using System.Text.RegularExpressions;

namespace AqsPluginExtDb.Core.Validation;

/// <summary>
/// Validates callable names (stored procedure/function identifiers) before they are
/// interpolated into a CALL/EXEC statement, since they can't be passed as bind parameters.
/// </summary>
public static partial class IdentifierValidator
{
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    private static partial Regex SafeIdentifierRegex();

    public static bool IsSafeIdentifier(string value) => SafeIdentifierRegex().IsMatch(value);
}
