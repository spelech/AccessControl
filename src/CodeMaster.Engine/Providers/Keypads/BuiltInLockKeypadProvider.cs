using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CodeMaster.Core.DTOs;
using CodeMaster.Core.Interfaces;
using CodeMaster.Core.Models;
using CodeMaster.Engine.Mqtt;

namespace CodeMaster.Engine.Providers.Keypads;

public class BuiltInLockKeypadProvider : IKeypadProvider
{
    private readonly string _baseTopic;
    private readonly IMqttClientService? _mqttClient;

    public KeypadCapabilities Capabilities => KeypadCapabilities.SlottedPinStorage;

    public KeypadMode Mode => KeypadMode.HardwareSlotted;

    public BuiltInLockKeypadProvider(string baseTopic = "zwave", IMqttClientService? mqttClient = null)
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

        // Must be a Z-Wave notification, alarm, or access control topic
        if (!topic.Contains("alarm", StringComparison.OrdinalIgnoreCase) &&
            !topic.Contains("notification", StringComparison.OrdinalIgnoreCase) &&
            !topic.Contains("access_control", StringComparison.OrdinalIgnoreCase) &&
            !topic.StartsWith("zwave/", StringComparison.OrdinalIgnoreCase))
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

            int? alarmType = null;
            if (TryGetIntProperty(root, "type", out var t) ||
                TryGetIntProperty(root, "alarm_type", out t) ||
                TryGetIntProperty(root, "alarmType", out t) ||
                TryGetIntProperty(root, "access_control", out t) ||
                TryGetIntProperty(root, "accessControl", out t) ||
                TryGetIntProperty(root, "event", out t) ||
                TryGetIntProperty(root, "notification", out t))
            {
                alarmType = t;
            }

            if (!alarmType.HasValue)
            {
                return false;
            }

            KeypadAction action;
            if (alarmType.Value is 19 or 6)
            {
                // Alarm type 19 or Access Control event 6 = Keypad unlock
                action = KeypadAction.Unlock;
            }
            else if (alarmType.Value is 18 or 5)
            {
                // Alarm type 18 or Access Control event 5 = Keypad lock
                action = KeypadAction.Lock;
            }
            else
            {
                return false;
            }

            // Extract slot number
            int? slot = null;
            if (TryGetIntProperty(root, "slot", out var s) ||
                TryGetIntProperty(root, "userId", out s) ||
                TryGetIntProperty(root, "user_id", out s) ||
                TryGetIntProperty(root, "user", out s) ||
                TryGetIntProperty(root, "code_slot", out s))
            {
                slot = s;
            }
            else if (root.TryGetProperty("eventParameters", out var eventParams) && eventParams.ValueKind == JsonValueKind.Object)
            {
                if (TryGetIntProperty(eventParams, "userId", out var eps) ||
                    TryGetIntProperty(eventParams, "slot", out eps))
                {
                    slot = eps;
                }
            }

            var deviceId = ExtractDeviceIdFromTopic(topic);

            keypadEvent = new KeypadEventDto
            {
                Action = action,
                SlotNumber = slot,
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

    private static bool TryGetIntProperty(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value))
            {
                return true;
            }
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value))
            {
                return true;
            }
        }
        return false;
    }

    private static string? ExtractDeviceIdFromTopic(string topic)
    {
        var parts = topic.Split('/');
        if (parts.Length > 1 && parts[0].Equals("zwave", StringComparison.OrdinalIgnoreCase))
        {
            return parts[1];
        }
        return null;
    }
}
