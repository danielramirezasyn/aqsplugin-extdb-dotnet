using System.Data;
using AqsPluginExtDb.Core.Options;
using AqsPluginExtDb.Models;
using MySqlConnector;

namespace AqsPluginExtDb.Drivers;

public sealed class MySqlDriver : IDbDriver
{
    public string DriverName => "mysql";

    public string BuildConnectionString(ConnectionAlias alias, string plainPassword, PoolOptions pool)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = alias.Host,
            Port = (uint)alias.Port,
            Database = alias.Database,
            UserID = alias.User,
            Password = plainPassword,
            Pooling = pool.Enabled,
            MinimumPoolSize = (uint)pool.MinSize,
            MaximumPoolSize = (uint)pool.MaxSize,
            ConnectionTimeout = (uint)pool.TimeoutSeconds
        };

        ApplyDriverOptions(builder, alias.DriverOptions);

        return builder.ConnectionString;
    }

    public async Task<DriverExecutionResult> ExecuteQueryAsync(string connectionString, string statement, IReadOnlyList<object?> parameters, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = statement;
        SqlExecutionHelper.BindParameters(command, parameters);

        return await SqlExecutionHelper.ExecuteAndReadAsync(command, ct);
    }

    public async Task<DriverExecutionResult> ExecuteCallableAsync(string connectionString, string procedureName, IReadOnlyList<object?> parameters, CancellationToken ct)
    {
        string placeholders = string.Join(", ", Enumerable.Range(0, parameters.Count).Select(i => $"@p{i}"));
        string statement = $"CALL {procedureName}({placeholders})";

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = statement;
        SqlExecutionHelper.BindParameters(command, parameters);

        return await SqlExecutionHelper.ExecuteAndReadAsync(command, ct);
    }

    private static void ApplyDriverOptions(MySqlConnectionStringBuilder builder, Dictionary<string, string>? options)
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
