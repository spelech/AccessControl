using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace AccessControl.Engine.Services;

public record DiscoveredTopic(
    string Topic,
    string DeviceType,
    string Description,
    DateTimeOffset LastSeen,
    string? SamplePayload = null);

public record DiscoveredContactSensor(
    string Topic,
    string DeviceName,
    string Integration,
    string Model,
    string CurrentState,
    DateTimeOffset LastSeen);

public record SnifferEvent(
    string Topic,
    string DeviceName,
    string Model,
    string State,
    DateTimeOffset Timestamp);

public record SnifferResult(
    bool Detected,
    SnifferEvent? Event = null);

public interface IMqttDiscoveryService
{
    void RecordTopic(string topic, string? payload = null);
    IReadOnlyList<DiscoveredTopic> GetDiscoveredTopics(string? deviceType = null);
    IReadOnlyList<DiscoveredContactSensor> GetDiscoveredContactSensors();
    SnifferResult SniffActivity(DateTimeOffset since);
}

public class MqttTopicDiscoveryService : IMqttDiscoveryService
{
    private readonly ConcurrentDictionary<string, DiscoveredTopic> _discovered = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DiscoveredContactSensor> _contactSensors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<SnifferEvent> _snifferEvents = new();
    private const int MaxSnifferEvents = 50;

    private static readonly HashSet<string> BlacklistedMetrics = new(StringComparer.OrdinalIgnoreCase)
    {
        "battery", "linkquality", "temperature", "temp", "humidity",
        "illuminance", "power", "voltage", "energy", "action",
        "update", "tamper", "rssi", "motion", "occupancy"
    };

    public MqttTopicDiscoveryService()
    {
        // Seed default well-known discovery topics so UI and API always have immediate choices
        RecordTopic("zwave/front_door_lock/door_lock/endpoint_0/currentState", "{\"value\":\"locked\"}");
        RecordTopic("zwave/side_door/door_lock/endpoint_0/currentState", "{\"value\":\"locked\"}");
        RecordTopic("ring/front_keypad/alarm/command", "{\"command\":\"disarm\",\"code\":\"\"}");

        // Seed default contact sensors so the UI always has realistic choices even before live traffic
        var seedTime = DateTimeOffset.UtcNow;
        _contactSensors["zigbee2mqtt/front_door_contact"] = new DiscoveredContactSensor(
            "zigbee2mqtt/front_door_contact",
            "Front Door Contact",
            "Zigbee2MQTT",
            "Aqara MCCGQ11LM",
            "closed",
            seedTime);

        _contactSensors["zigbee2mqtt/side_entry_contact"] = new DiscoveredContactSensor(
            "zigbee2mqtt/side_entry_contact",
            "Side Entry Contact",
            "Zigbee2MQTT",
            "Aqara MCCGQ11LM",
            "closed",
            seedTime);

        _contactSensors["homeassistant/binary_sensor/patio_door/state"] = new DiscoveredContactSensor(
            "homeassistant/binary_sensor/patio_door/state",
            "Patio Door Contact",
            "Home Assistant",
            "Ecolink Door Sensor",
            "closed",
            seedTime);

        _discovered["zigbee2mqtt/front_door_contact"] = new DiscoveredTopic("zigbee2mqtt/front_door_contact", "sensor", "Contact Sensor (Front Door Contact)", seedTime, "{\"contact\":true}");
        _discovered["zigbee2mqtt/side_entry_contact"] = new DiscoveredTopic("zigbee2mqtt/side_entry_contact", "sensor", "Contact Sensor (Side Entry Contact)", seedTime, "{\"contact\":true}");
        _discovered["homeassistant/binary_sensor/patio_door/state"] = new DiscoveredTopic("homeassistant/binary_sensor/patio_door/state", "sensor", "Contact Sensor (Patio Door Contact)", seedTime, "OFF");
    }

    public void RecordTopic(string topic, string? payload = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var (deviceType, description) = ClassifyTopic(topic);
        var entry = new DiscoveredTopic(topic, deviceType, description, now, payload);
        _discovered[topic] = entry;

        // Contact sensor processing
        if (TryProcessContactSensor(topic, payload, now, out var contactSensor, out var snifferEvent))
        {
            _contactSensors[topic] = contactSensor;
            if (snifferEvent != null)
            {
                _snifferEvents.Enqueue(snifferEvent);
                while (_snifferEvents.Count > MaxSnifferEvents && _snifferEvents.TryDequeue(out _))
                {
                }
            }
        }
    }

    public IReadOnlyList<DiscoveredTopic> GetDiscoveredTopics(string? deviceType = null)
    {
        var query = _discovered.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(deviceType))
        {
            query = query.Where(t => string.Equals(t.DeviceType, deviceType, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(t => t.Topic).ToList();
    }

    public IReadOnlyList<DiscoveredContactSensor> GetDiscoveredContactSensors()
    {
        return _contactSensors.Values.OrderBy(s => s.Topic).ToList();
    }

    public SnifferResult SniffActivity(DateTimeOffset since)
    {
        var newest = _snifferEvents
            .Where(e => e.Timestamp > since)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault();

        return newest != null
            ? new SnifferResult(true, newest)
            : new SnifferResult(false);
    }

    private static bool HasBlacklistedMetric(string topic)
    {
        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            if (BlacklistedMetrics.Contains(seg))
            {
                return true;
            }

            var tokens = seg.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (BlacklistedMetrics.Contains(token))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryProcessContactSensor(
        string topic,
        string? payload,
        DateTimeOffset timestamp,
        [NotNullWhen(true)] out DiscoveredContactSensor? contactSensor,
        [NotNullWhen(true)] out SnifferEvent? snifferEvent)
    {
        contactSensor = null;
        snifferEvent = null;

        if (HasBlacklistedMetric(topic))
        {
            return false;
        }

        bool matchesTopicPattern =
            topic.EndsWith("/contact", StringComparison.OrdinalIgnoreCase) ||
            topic.EndsWith("_contact", StringComparison.OrdinalIgnoreCase) ||
            topic.Contains("door_contact", StringComparison.OrdinalIgnoreCase) ||
            topic.Contains("contact_sensor", StringComparison.OrdinalIgnoreCase);

        bool hasJsonContact = false;
        bool? jsonContactBool = null;
        string? jsonState = null;
        string? friendlyNameFromJson = null;
        string? modelFromJson = null;

        if (!string.IsNullOrWhiteSpace(payload))
        {
            var trimmed = payload.Trim();
            if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) || (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("contact", out var cp))
                        {
                            if (cp.ValueKind is JsonValueKind.True or JsonValueKind.False)
                            {
                                hasJsonContact = true;
                                jsonContactBool = cp.GetBoolean();
                            }
                            else if (cp.ValueKind == JsonValueKind.String && bool.TryParse(cp.GetString(), out var parsedBool))
                            {
                                hasJsonContact = true;
                                jsonContactBool = parsedBool;
                            }
                        }

                        if (root.TryGetProperty("state", out var sp) && sp.ValueKind == JsonValueKind.String)
                        {
                            jsonState = sp.GetString();
                        }

                        if (root.TryGetProperty("friendly_name", out var fn) && fn.ValueKind == JsonValueKind.String)
                        {
                            friendlyNameFromJson = fn.GetString();
                        }
                        else if (root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                        {
                            friendlyNameFromJson = n.GetString();
                        }
                        else if (root.TryGetProperty("device", out var dev) && dev.ValueKind == JsonValueKind.Object)
                        {
                            if (dev.TryGetProperty("name", out var dn) && dn.ValueKind == JsonValueKind.String)
                            {
                                friendlyNameFromJson = dn.GetString();
                            }
                            if (dev.TryGetProperty("model", out var dm) && dm.ValueKind == JsonValueKind.String)
                            {
                                modelFromJson = dm.GetString();
                            }
                        }

                        if (root.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String)
                        {
                            modelFromJson = m.GetString();
                        }
                    }
                }
                catch (JsonException)
                {
                    // Ignore malformed JSON
                }
            }
        }

        bool isBinarySensorTopic = topic.Contains("binary_sensor", StringComparison.OrdinalIgnoreCase);

        bool isBinaryPayload = false;
        string? rawBinaryState = null;
        if (!string.IsNullOrWhiteSpace(payload))
        {
            var trimmed = payload.Trim().Trim('"');
            if (string.Equals(trimmed, "ON", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "OPEN", StringComparison.OrdinalIgnoreCase))
            {
                isBinaryPayload = true;
                rawBinaryState = "OPEN";
            }
            else if (string.Equals(trimmed, "OFF", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(trimmed, "CLOSED", StringComparison.OrdinalIgnoreCase))
            {
                isBinaryPayload = true;
                rawBinaryState = "CLOSED";
            }
        }

        bool qualifies = matchesTopicPattern ||
                         hasJsonContact ||
                         (isBinarySensorTopic && (isBinaryPayload || !string.IsNullOrWhiteSpace(jsonState)));

        if (!qualifies)
        {
            return false;
        }

        string stateUpper = "CLOSED";
        if (hasJsonContact && jsonContactBool.HasValue)
        {
            // Zigbee2MQTT standard: contact:true -> CLOSED, contact:false -> OPEN
            stateUpper = jsonContactBool.Value ? "CLOSED" : "OPEN";
        }
        else if (!string.IsNullOrWhiteSpace(jsonState))
        {
            if (string.Equals(jsonState, "ON", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(jsonState, "OPEN", StringComparison.OrdinalIgnoreCase))
            {
                stateUpper = "OPEN";
            }
            else
            {
                stateUpper = "CLOSED";
            }
        }
        else if (!string.IsNullOrWhiteSpace(rawBinaryState))
        {
            stateUpper = rawBinaryState;
        }

        string currentState = stateUpper.ToLowerInvariant();
        string integration = DeriveIntegration(topic);
        string deviceName = !string.IsNullOrWhiteSpace(friendlyNameFromJson)
            ? friendlyNameFromJson
            : DeriveDeviceNameFromTopic(topic);
        string model = !string.IsNullOrWhiteSpace(modelFromJson)
            ? modelFromJson
            : DeriveModel(topic, deviceName, integration);

        contactSensor = new DiscoveredContactSensor(
            topic,
            deviceName,
            integration,
            model,
            currentState,
            timestamp);

        snifferEvent = new SnifferEvent(
            topic,
            deviceName,
            model,
            stateUpper,
            timestamp);

        return true;
    }

    private static string DeriveIntegration(string topic)
    {
        if (topic.StartsWith("zigbee2mqtt", StringComparison.OrdinalIgnoreCase))
        {
            return "Zigbee2MQTT";
        }
        if (topic.StartsWith("homeassistant", StringComparison.OrdinalIgnoreCase))
        {
            return "Home Assistant";
        }
        if (topic.StartsWith("zwave", StringComparison.OrdinalIgnoreCase))
        {
            return "Z-Wave JS";
        }
        if (topic.StartsWith("ring", StringComparison.OrdinalIgnoreCase))
        {
            return "Ring MQTT";
        }
        return "Generic MQTT";
    }

    private static string DeriveDeviceNameFromTopic(string topic)
    {
        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string rawName = topic;

        if (segments.Length >= 2)
        {
            if (string.Equals(segments[^1], "state", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segments[^1], "contact", StringComparison.OrdinalIgnoreCase))
            {
                rawName = segments.Length >= 3 ? segments[^2] : segments[1];
            }
            else
            {
                rawName = segments[1];
            }
        }

        var words = rawName.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(w => char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : string.Empty))
                           .ToList();

        var title = string.Join(" ", words);

        if (!title.EndsWith("Contact", StringComparison.OrdinalIgnoreCase) &&
            !title.EndsWith("Sensor", StringComparison.OrdinalIgnoreCase))
        {
            title += " Contact";
        }

        return title;
    }

    private static string DeriveModel(string topic, string deviceName, string integration)
    {
        var combined = (topic + " " + deviceName).ToLowerInvariant();
        if (combined.Contains("aqara") || integration == "Zigbee2MQTT")
        {
            return "Aqara MCCGQ11LM";
        }
        if (combined.Contains("ecolink") || integration == "Home Assistant")
        {
            return "Ecolink Door Sensor";
        }
        if (combined.Contains("ring"))
        {
            return "Ring Contact Sensor";
        }
        return "Generic Door Contact";
    }

    private static (string DeviceType, string Description) ClassifyTopic(string topic)
    {
        var lower = topic.ToLowerInvariant();

        if (lower.Contains("lock") || lower.Contains("deadbolt") || lower.Contains("bolt"))
        {
            return ("lock", FormatDescription("Smart Lock", topic));
        }

        if (lower.Contains("keypad") || lower.Contains("alarm") || lower.Contains("user_code") || lower.Contains("usercode"))
        {
            return ("keypad", FormatDescription("Security Keypad", topic));
        }

        if (lower.Contains("contact") || lower.Contains("door") || lower.Contains("sensor") || lower.Contains("binary_sensor"))
        {
            return ("sensor", FormatDescription("Contact Sensor", topic));
        }

        return ("other", FormatDescription("MQTT Entity", topic));
    }

    private static string FormatDescription(string category, string topic)
    {
        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var name = parts.Length > 1 ? parts[1].Replace('_', ' ') : topic;
        return $"{category} ({name})";
    }
}
