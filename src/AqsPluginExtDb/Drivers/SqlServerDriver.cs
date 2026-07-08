using System.Data;
using AqsPluginExtDb.Core.Options;
using AqsPluginExtDb.Models;
using Microsoft.Data.SqlClient;

namespace AqsPluginExtDb.Drivers;

public sealed class SqlServerDriver : IDbDriver
{
    public string DriverName => "sqlserver";

    public string BuildConnectionString(ConnectionAlias alias, string plainPassword, PoolOptions pool)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{alias.Host},{alias.Port}",
            InitialCatalog = alias.Database,
            UserID = alias.User,
            Password = plainPassword,
            Pooling = pool.Enabled,
            MinPoolSize = pool.MinSize,
            MaxPoolSize = pool.MaxSize,
            ConnectTimeout = pool.TimeoutSeconds,
            TrustServerCertificate = true
        };

        ApplyDriverOptions(builder, alias.DriverOptions);

        return builder.ConnectionString;
    }

    public async Task<DriverExecutionResult> ExecuteQueryAsync(string connectionString, string statement, IReadOnlyList<object?> parameters, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = statement;
        SqlExecutionHelper.BindParameters(command, parameters);

        return await SqlExecutionHelper.ExecuteAndReadAsync(command, ct);
    }

    public async Task<DriverExecutionResult> ExecuteCallableAsync(string connectionString, string procedureName, IReadOnlyList<object?> parameters, CancellationToken ct)
    {
        // Built as a literal EXEC instead of CommandType.StoredProcedure so parameters bind
        // positionally by @p{i}; SqlClient's StoredProcedure mode instead requires parameter
        // names to match the procedure's declared formal parameter names.
        string placeholders = string.Join(", ", Enumerable.Range(0, parameters.Count).Select(i => $"@p{i}"));
        string statement = parameters.Count > 0 ? $"EXEC {procedureName} {placeholders}" : $"EXEC {procedureName}";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = statement;
        SqlExecutionHelper.BindParameters(command, parameters);

        return await SqlExecutionHelper.ExecuteAndReadAsync(command, ct);
    }

    private static void ApplyDriverOptions(SqlConnectionStringBuilder builder, Dictionary<string, string>? options)
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
