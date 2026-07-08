using System.Data;
using AqsPluginExtDb.Core.Options;
using AqsPluginExtDb.Models;
using Npgsql;

namespace AqsPluginExtDb.Drivers;

public sealed class PostgresDriver : IDbDriver
{
    public string DriverName => "postgresql";

    public string BuildConnectionString(ConnectionAlias alias, string plainPassword, PoolOptions pool)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = alias.Host,
            Port = alias.Port,
            Database = alias.Database,
            Username = alias.User,
            Password = plainPassword,
            Pooling = pool.Enabled,
            MinPoolSize = pool.MinSize,
            MaxPoolSize = pool.MaxSize,
            Timeout = pool.TimeoutSeconds
        };

        ApplyDriverOptions(builder, alias.DriverOptions);

        return builder.ConnectionString;
    }

    public async Task<DriverExecutionResult> ExecuteQueryAsync(string connectionString, string statement, IReadOnlyList<object?> parameters, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = statement;
        SqlExecutionHelper.BindParameters(command, parameters);

        return await SqlExecutionHelper.ExecuteAndReadAsync(command, ct);
    }

    public async Task<DriverExecutionResult> ExecuteCallableAsync(string connectionString, string procedureName, IReadOnlyList<object?> parameters, CancellationToken ct)
    {
        // Requires the target to be an actual PostgreSQL PROCEDURE (PG11+, CREATE PROCEDURE).
        // To invoke a FUNCTION instead, use "query" mode with "SELECT * FROM fn(@p0, ...)".
        string placeholders = string.Join(", ", Enumerable.Range(0, parameters.Count).Select(i => $"@p{i}"));
        string statement = $"CALL {procedureName}({placeholders})";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = statement;
        SqlExecutionHelper.BindParameters(command, parameters);

        return await SqlExecutionHelper.ExecuteAndReadAsync(command, ct);
    }

    private static void ApplyDriverOptions(NpgsqlConnectionStringBuilder builder, Dictionary<string, string>? options)
    {
        if (options is null)
        {
            return;
        }

        foreach (var (key, value) in options)
        {
            builder[key] = value;
        }
    }
}
