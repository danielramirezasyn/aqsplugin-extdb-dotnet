using System.Text.Json.Serialization;

namespace AqsPluginExtDb.Models;

public sealed class SetupRequest
{
    [JsonPropertyName("alias")]
    public required string Alias { get; init; }

    [JsonPropertyName("db_type")]
    public required string DbType { get; init; }

    [JsonPropertyName("host")]
    public required string Host { get; init; }

    [JsonPropertyName("port")]
    public required int Port { get; init; }

    [JsonPropertyName("database")]
    public required string Database { get; init; }

    [JsonPropertyName("user")]
    public required string User { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }

    [JsonPropertyName("driver_options")]
    public Dictionary<string, string>? DriverOptions { get; init; }
}
