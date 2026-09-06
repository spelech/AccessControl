using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeMaster.Engine.Transports;

public record ZWaveJsVersionInfo(
    [property: JsonPropertyName("driverVersion")] string? DriverVersion,
    [property: JsonPropertyName("serverVersion")] string? ServerVersion,
    [property: JsonPropertyName("homeId")] long? HomeId,
    [property: JsonPropertyName("minSchemaVersion")] int? MinSchemaVersion,
    [property: JsonPropertyName("maxSchemaVersion")] int? MaxSchemaVersion
);

public record ZWaveNodeSummary(
    int NodeId,
    string Name,
    string DeviceType, // "lock", "keypad", "sensor", "unknown"
    string? Model = null
);

public class ZWaveJsCommandRequest
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("nodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NodeId { get; set; }

    [JsonPropertyName("endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Endpoint { get; set; }

    [JsonPropertyName("commandClass")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CommandClass { get; set; }

    [JsonPropertyName("method")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Method { get; set; }

    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object?[]? Args { get; set; }

    [JsonPropertyName("valueId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ValueId { get; set; }

    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Value { get; set; }
}

public class ZWaveJsResultResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("messageId")]
    public string? MessageId { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public JsonElement Result { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }
}
