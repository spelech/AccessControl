using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;

namespace AccessControl.Engine.Providers.Sensors;

public class MqttContactSensorOptions
{
    public string? Topic { get; set; }
    public string OpenPayload { get; set; } = "ON";
    public string ClosedPayload { get; set; } = "OFF";
    public bool Invert { get; set; }
}

public class MqttContactSensorProvider : IDoorSensorProvider
{
    private readonly MqttContactSensorOptions _options;

    public MqttContactSensorOptions Options => _options;

    public MqttContactSensorProvider(MqttContactSensorOptions? options = null)
    {
        _options = options ?? new MqttContactSensorOptions();
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public bool TryParseContactEvent(MqttInboundMessage message, [NotNullWhen(true)] out DoorContactState? contactState)
    {
        return TryParseContactEvent(message.Topic, message.Payload, out contactState);
    }

    public bool TryParseContactEvent(string topic, string payload, [NotNullWhen(true)] out DoorContactState? contactState)
    {
        contactState = null;

        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_options.Topic) &&
            !string.Equals(topic, _options.Topic, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var trimmed = payload.Trim();
        DoorContactState? rawState = null;

        // Direct payload string comparison
        if (string.Equals(trimmed, _options.OpenPayload, StringComparison.OrdinalIgnoreCase))
        {
            rawState = DoorContactState.Open;
        }
        else if (string.Equals(trimmed, _options.ClosedPayload, StringComparison.OrdinalIgnoreCase))
        {
            rawState = DoorContactState.Closed;
        }
        else if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            // JSON payload inspection
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    string? stateStr = null;
                    if (root.TryGetProperty("state", out var p) ||
                        root.TryGetProperty("contact", out p) ||
                        root.TryGetProperty("value", out p))
                    {
                        if (p.ValueKind == JsonValueKind.String)
                        {
                            stateStr = p.GetString();
                        }
                        else if (p.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        {
                            stateStr = p.GetBoolean() ? "true" : "false";
                        }
                        else if (p.ValueKind == JsonValueKind.Number)
                        {
                            stateStr = p.GetInt32().ToString();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(stateStr))
                    {
                        if (string.Equals(stateStr, _options.OpenPayload, StringComparison.OrdinalIgnoreCase))
                        {
                            rawState = DoorContactState.Open;
                        }
                        else if (string.Equals(stateStr, _options.ClosedPayload, StringComparison.OrdinalIgnoreCase))
                        {
                            rawState = DoorContactState.Closed;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore parse errors on malformed json
            }
        }

        if (!rawState.HasValue)
        {
            return false;
        }

        var finalState = rawState.Value;
        if (_options.Invert)
        {
            finalState = finalState == DoorContactState.Open
                ? DoorContactState.Closed
                : DoorContactState.Open;
        }

        contactState = finalState;
        return true;
    }
}
