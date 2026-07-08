using System.Text.Json.Serialization;

namespace AqsPluginExtDb.Models;

public sealed class SetupResponseItem
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

    [JsonPropertyName("driver_options")]
    public Dictionary<string, string>? DriverOptions { get; init; }

    public static SetupResponseItem FromConnectionAlias(ConnectionAlias alias) => new()
    {
        Alias = alias.Alias,
        DbType = alias.DbType,
        Host = alias.Host,
        Port = alias.Port,
        Database = alias.Database,
        User = alias.User,
        DriverOptions = alias.DriverOptions
    };
}
