using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AccessControl.Core.DTOs;
using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;
using AccessControl.Engine.Mqtt;

namespace AccessControl.Engine.Providers.Keypads;

public class RingMqttKeypadProvider : IKeypadProvider
{
    private readonly string _baseTopic;
    private readonly IMqttClientService? _mqttClient;

    public KeypadCapabilities Capabilities =>
        KeypadCapabilities.SupportsPin |
        KeypadCapabilities.SupportsArmModes;

    public KeypadMode Mode => KeypadMode.StatelessEvent;

    public RingMqttKeypadProvider(string baseTopic = "ring", IMqttClientService? mqttClient = null)
    {
        _baseTopic = baseTopic.Trim().TrimEnd('/');
        _mqttClient = mqttClient;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public bool TryParseKeypadEvent(MqttInboundMessage message, [NotNullWhen(true)] out KeypadEventDto? keypadEvent)
    {
        return TryParseKeypadEvent(message.Topic, message.Payload, out keypadEvent);
    }

    public bool TryParseKeypadEvent(string topic, string payload, [NotNullWhen(true)] out KeypadEventDto? keypadEvent)
    {
        keypadEvent = null;

        if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        // Check if topic is a relevant ring alarm/keypad command topic or matches configured base topic
        if (!topic.Contains("command", StringComparison.OrdinalIgnoreCase) &&
            !topic.Contains("keypad", StringComparison.OrdinalIgnoreCase) &&
            !topic.Contains("alarm", StringComparison.OrdinalIgnoreCase) &&
            !topic.Contains("kp", StringComparison.OrdinalIgnoreCase) &&
            !topic.StartsWith(_baseTopic, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // Command / action property
            string? commandStr = null;
            if (root.TryGetProperty("command", out var cmdProp) ||
                root.TryGetProperty("action", out cmdProp) ||
                root.TryGetProperty("mode", out cmdProp))
            {
                commandStr = cmdProp.GetString();
            }

            if (string.IsNullOrWhiteSpace(commandStr))
            {
                return false;
            }

            var action = ParseAction(commandStr);
            if (action == null)
            {
                return false;
            }

            // PIN / Code property
            string? pin = null;
            if (root.TryGetProperty("code", out var codeProp) ||
                root.TryGetProperty("pin", out codeProp) ||
                root.TryGetProperty("user_code", out codeProp))
            {
                pin = codeProp.GetString();
            }

            // DeviceId property or inferred from topic
            string? deviceId = null;
            if (root.TryGetProperty("device_id", out var devProp) ||
                root.TryGetProperty("deviceId", out devProp))
            {
                deviceId = devProp.GetString();
            }
            else
            {
                deviceId = ExtractDeviceIdFromTopic(topic);
            }

            keypadEvent = new KeypadEventDto
            {
                Action = action.Value,
                Pin = pin,
                DeviceId = deviceId,
                RawTopic = topic,
                Timestamp = DateTime.UtcNow
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static KeypadAction? ParseAction(string command)
    {
        return command.Trim().ToLowerInvariant() switch
        {
            "disarm" or "disarmed" => KeypadAction.Disarm,
            "arm_away" or "away" or "armed_away" => KeypadAction.ArmAway,
            "arm_stay" or "arm_home" or "stay" or "home" or "armed_stay" or "armed_home" => KeypadAction.ArmStay,
            "lock" => KeypadAction.Lock,
            "unlock" => KeypadAction.Unlock,
            "custom" => KeypadAction.Custom,
            _ => null
        };
    }

    private static string? ExtractDeviceIdFromTopic(string topic)
    {
        var parts = topic.Split('/');
        if (parts.Length > 2)
        {
            return parts[1]; // e.g. ring/<location_or_device_id>/...
        }
        return null;
    }
}
