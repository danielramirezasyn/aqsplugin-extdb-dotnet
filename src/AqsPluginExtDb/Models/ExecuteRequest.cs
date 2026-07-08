using System.Text.Json.Serialization;

namespace AqsPluginExtDb.Models;

public sealed class ExecuteRequest
{
    [JsonPropertyName("alias")]
    public required string Alias { get; init; }

    [JsonPropertyName("mode")]
    public required ExecutionMode Mode { get; init; }

    [JsonPropertyName("statement")]
    public required string Statement { get; init; }

    [JsonPropertyName("params")]
    public List<object?>? Params { get; init; }
}
