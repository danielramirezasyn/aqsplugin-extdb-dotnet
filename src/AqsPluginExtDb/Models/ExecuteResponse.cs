using System.Text.Json.Serialization;

namespace AqsPluginExtDb.Models;

public sealed class ExecuteResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("rows_affected")]
    public int RowsAffected { get; init; }

    [JsonPropertyName("columns")]
    public IReadOnlyList<string> Columns { get; init; } = [];

    [JsonPropertyName("data")]
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Data { get; init; } = [];

    [JsonPropertyName("execution_ms")]
    public long ExecutionMs { get; init; }

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    public static ExecuteResponse Ok(Drivers.DriverExecutionResult result, long executionMs) => new()
    {
        Status = "ok",
        RowsAffected = result.RowsAffected,
        Columns = result.Columns,
        Data = result.Data,
        ExecutionMs = executionMs
    };

    public static ExecuteResponse Error(string errorCode, string errorMessage, long executionMs = 0) => new()
    {
        Status = "error",
        ExecutionMs = executionMs,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
