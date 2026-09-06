using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeMaster.Mcp.Protocol;

public static class McpProtocolConstants
{
    public const string DefaultProtocolVersion = "2026-07-28";
    public static readonly string[] SupportedProtocolVersions = ["2026-07-28", "2024-11-05", "2024-10-07"];

    public static string NegotiateVersion(string? requestedVersion)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
        {
            return DefaultProtocolVersion;
        }

        if (SupportedProtocolVersions.Contains(requestedVersion, StringComparer.OrdinalIgnoreCase))
        {
            return requestedVersion;
        }

        return DefaultProtocolVersion;
    }
}

public class McpRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

public class McpRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpRpcError? Error { get; set; }

    public static McpRpcResponse Success(object? id, object? result) =>
        new() { Id = id, Result = result };

    public static McpRpcResponse CreateError(object? id, int code, string message, object? data = null) =>
        new() { Id = id, Error = new McpRpcError { Code = code, Message = message, Data = data } };
}

public class McpRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

public class McpToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public object InputSchema { get; set; } = new();
}

public class McpToolCallResult
{
    [JsonPropertyName("content")]
    public List<McpContentItem> Content { get; set; } = [];

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }

    public static McpToolCallResult Text(string text, bool isError = false) =>
        new() { Content = [new McpContentItem { Type = "text", Text = text }], IsError = isError };
}

public class McpContentItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
