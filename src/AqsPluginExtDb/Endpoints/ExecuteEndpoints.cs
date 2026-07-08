using System.Diagnostics;
using AqsPluginExtDb.Core.Crypto;
using AqsPluginExtDb.Core.Options;
using AqsPluginExtDb.Core.Storage;
using AqsPluginExtDb.Core.Validation;
using AqsPluginExtDb.Drivers;
using AqsPluginExtDb.Models;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace AqsPluginExtDb.Endpoints;

public static class ExecuteEndpoints
{
    public static void MapExecuteEndpoints(this WebApplication app)
    {
        app.MapPost("/execute", ExecuteAsync);
    }

    private static async Task<IResult> ExecuteAsync(
        ExecuteRequest request,
        DriverRegistry drivers,
        ConnectionStore store,
        CryptoService crypto,
        PoolOptions poolOptions,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.Statement))
        {
            return Results.Ok(ExecuteResponse.Error("VALIDATION_ERROR", "statement is required.", stopwatch.ElapsedMilliseconds));
        }

        if (request.Mode == ExecutionMode.Callable && !IdentifierValidator.IsSafeIdentifier(request.Statement))
        {
            return Results.Ok(ExecuteResponse.Error("VALIDATION_ERROR", "statement must be a valid procedure identifier for callable mode.", stopwatch.ElapsedMilliseconds));
        }

        var alias = await store.GetAsync(request.Alias, ct);
        if (alias is null)
        {
            return Results.Ok(ExecuteResponse.Error("ALIAS_NOT_FOUND", $"Alias '{request.Alias}' is not registered.", stopwatch.ElapsedMilliseconds));
        }

        if (!drivers.TryGet(alias.DbType, out var driver) || driver is null)
        {
            return Results.Ok(ExecuteResponse.Error("UNSUPPORTED_DRIVER", $"db_type '{alias.DbType}' is not supported.", stopwatch.ElapsedMilliseconds));
        }

        string plainPassword;
        try
        {
            plainPassword = crypto.Decrypt(alias.EncryptedPassword);
        }
        catch (CryptoException ex)
        {
            logger.LogError(ex, "Failed to decrypt password for alias {Alias}", request.Alias);
            return Results.Ok(ExecuteResponse.Error("DECRYPTION_ERROR", "Failed to decrypt stored credentials.", stopwatch.ElapsedMilliseconds));
        }

        string connectionString = driver.BuildConnectionString(alias, plainPassword, poolOptions);
        IReadOnlyList<object?> parameters = request.Params ?? [];

        try
        {
            DriverExecutionResult result = request.Mode switch
            {
                ExecutionMode.Query => await driver.ExecuteQueryAsync(connectionString, request.Statement, parameters, ct),
                ExecutionMode.Callable => await driver.ExecuteCallableAsync(connectionString, request.Statement, parameters, ct),
                _ => throw new InvalidOperationException($"Unsupported mode '{request.Mode}'.")
            };

            stopwatch.Stop();
            return Results.Ok(ExecuteResponse.Ok(result, stopwatch.ElapsedMilliseconds));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Execution failed for alias {Alias} in mode {Mode}", request.Alias, request.Mode);
            var (code, message) = ClassifyError(ex);
            return Results.Ok(ExecuteResponse.Error(code, message, stopwatch.ElapsedMilliseconds));
        }
    }

    private static (string Code, string Message) ClassifyError(Exception ex) => ex switch
    {
        SqlException { Number: -2 } => ("TIMEOUT", "The database operation timed out."),
        SqlException sqlEx => ("QUERY_FAILED", sqlEx.Message),
        MySqlException mySqlEx => ("QUERY_FAILED", mySqlEx.Message),
        PostgresException pgEx => ("QUERY_FAILED", pgEx.MessageText),
        TimeoutException => ("TIMEOUT", "The database operation timed out."),
        _ => ("CONNECTION_FAILED", ex.Message)
    };
}
