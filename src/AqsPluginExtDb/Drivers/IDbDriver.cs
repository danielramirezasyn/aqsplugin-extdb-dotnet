using AqsPluginExtDb.Core.Options;
using AqsPluginExtDb.Models;

namespace AqsPluginExtDb.Drivers;

/// <summary>
/// Strategy interface implemented by each database engine (SQL Server, MySQL, PostgreSQL).
/// </summary>
public interface IDbDriver
{
    /// <summary>Matches the db_type value used in /setup ("sqlserver" | "mysql" | "postgresql").</summary>
    string DriverName { get; }

    string BuildConnectionString(ConnectionAlias alias, string plainPassword, PoolOptions pool);

    Task<DriverExecutionResult> ExecuteQueryAsync(
        string connectionString,
        string statement,
        IReadOnlyList<object?> parameters,
        CancellationToken ct);

    Task<DriverExecutionResult> ExecuteCallableAsync(
        string connectionString,
        string procedureName,
        IReadOnlyList<object?> parameters,
        CancellationToken ct);
}

public sealed record DriverExecutionResult(
    int RowsAffected,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Data);
