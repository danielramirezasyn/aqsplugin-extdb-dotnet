using System.Text.Json.Serialization;

namespace AqsPluginExtDb.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionMode>))]
public enum ExecutionMode
{
    Query,
    Callable
}
