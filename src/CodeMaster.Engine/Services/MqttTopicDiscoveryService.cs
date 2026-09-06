using System.Collections.Concurrent;

namespace CodeMaster.Engine.Services;

public record DiscoveredTopic(
    string Topic,
    string DeviceType,
    string Description,
    DateTimeOffset LastSeen,
    string? SamplePayload = null);

public interface IMqttDiscoveryService
{
    void RecordTopic(string topic, string? payload = null);
    IReadOnlyList<DiscoveredTopic> GetDiscoveredTopics(string? deviceType = null);
}

public class MqttTopicDiscoveryService : IMqttDiscoveryService
{
    private readonly ConcurrentDictionary<string, DiscoveredTopic> _discovered = new(StringComparer.OrdinalIgnoreCase);

    public MqttTopicDiscoveryService()
    {
        // Seed default well-known discovery topics so UI and API always have immediate choices
        RecordTopic("zwave/front_door_lock/door_lock/endpoint_0/currentState", "{\"value\":\"locked\"}");
        RecordTopic("zwave/side_door/door_lock/endpoint_0/currentState", "{\"value\":\"locked\"}");
        RecordTopic("ring/front_keypad/alarm/command", "{\"command\":\"disarm\",\"code\":\"\"}");
        RecordTopic("zigbee2mqtt/front_door_contact/contact", "{\"contact\":true}");
    }

    public void RecordTopic(string topic, string? payload = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return;
        }

        var (deviceType, description) = ClassifyTopic(topic);
        var entry = new DiscoveredTopic(topic, deviceType, description, DateTimeOffset.UtcNow, payload);
        _discovered[topic] = entry;
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
