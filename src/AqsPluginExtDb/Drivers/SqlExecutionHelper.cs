using System.Data.Common;

namespace AqsPluginExtDb.Drivers;

/// <summary>
/// Provider-agnostic parameter binding and result-set hydration shared by all IDbDriver
/// implementations, since DbCommand/DbDataReader already abstract over the concrete provider.
/// </summary>
internal static class SqlExecutionHelper
{
    public static void BindParameters(DbCommand command, IReadOnlyList<object?> parameters)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = $"@p{i}";
            parameter.Value = ParameterConverter.ToProviderValue(parameters[i]);
            command.Parameters.Add(parameter);
        }
    }

    public static async Task<DriverExecutionResult> ExecuteAndReadAsync(DbCommand command, CancellationToken ct)
    {
        await using DbDataReader reader = await command.ExecuteReaderAsync(ct);
        return await ReadResultAsync(reader, ct);
    }

    /// <summary>
    /// Hydrates a DriverExecutionResult from any DbDataReader. Split out from
    /// ExecuteAndReadAsync so it can be exercised in tests against an in-memory
    /// DataTable.CreateDataReader() without a real database connection.
    /// </summary>
    internal static async Task<DriverExecutionResult> ReadResultAsync(DbDataReader reader, CancellationToken ct)
    {
        if (reader.FieldCount == 0)
        {
            while (await reader.NextResultAsync(ct))
            {
            }

            return new DriverExecutionResult(Math.Max(reader.RecordsAffected, 0), [], []);
        }

        var columns = new List<string>(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(columns.Count);
            for (int i = 0; i < columns.Count; i++)
            {
                object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[columns[i]] = value is DateTime dateTime ? dateTime.ToString("o") : value;
            }

            rows.Add(row);
        }

        int affected = reader.RecordsAffected;
        return new DriverExecutionResult(affected >= 0 ? affected : rows.Count, columns, rows);
    }
}
