using System.Text.Json;
using CodeMaster.Core.Models;

namespace CodeMaster.Engine.Services;

public record HaDiscoveryMessage(string Topic, string Payload);

public interface IHomeAssistantDiscoveryService
{
    string BuildEventDiscoveryTopic(AccessPoint door);
    string BuildEventDiscoveryPayload(AccessPoint door);
    string BuildLockDiscoveryTopic(AccessPoint door);
    string BuildLockDiscoveryPayload(AccessPoint door);
    string BuildSensorDiscoveryTopic(AccessPoint door);
    string BuildSensorDiscoveryPayload(AccessPoint door);
    string BuildAutoLockSwitchDiscoveryTopic(AccessPoint door);
    string BuildAutoLockSwitchDiscoveryPayload(AccessPoint door);
    IReadOnlyList<HaDiscoveryMessage> BuildAllDiscoveryMessages(AccessPoint door);
}

public class HomeAssistantDiscoveryService : IHomeAssistantDiscoveryService
{
    private readonly string _discoveryPrefix;
    private readonly string _statePrefix;

    public HomeAssistantDiscoveryService(string discoveryPrefix = "homeassistant", string statePrefix = "codemaster")
    {
        _discoveryPrefix = discoveryPrefix.TrimEnd('/');
        _statePrefix = statePrefix.TrimEnd('/');
    }

    public string BuildEventDiscoveryTopic(AccessPoint door) =>
        $"{_discoveryPrefix}/event/codemaster_{door.Id}/config";

    public string BuildEventDiscoveryPayload(AccessPoint door)
    {
        var topic = BuildEventDiscoveryTopic(door);
        var payload = new
        {
            discovery_topic = topic,
            name = $"{door.Name} Access",
            unique_id = $"codemaster_{door.Id}_access",
            state_topic = $"{_statePrefix}/{door.Id}/event/state",
            event_types = new[] { "keypad_unlock", "manual_unlock", "auto_lock", "tamper" },
            device = BuildDeviceObject(door)
        };

        return JsonSerializer.Serialize(payload);
    }

    public string BuildLockDiscoveryTopic(AccessPoint door) =>
        $"{_discoveryPrefix}/lock/codemaster_{door.Id}/config";

    public string BuildLockDiscoveryPayload(AccessPoint door)
    {
        var topic = BuildLockDiscoveryTopic(door);
        var payload = new
        {
            discovery_topic = topic,
            name = door.Name,
            unique_id = $"codemaster_{door.Id}_lock",
            state_topic = $"{_statePrefix}/{door.Id}/lock/state",
            command_topic = $"{_statePrefix}/{door.Id}/lock/set",
            payload_lock = "LOCK",
            payload_unlock = "UNLOCK",
            state_locked = "locked",
            state_unlocked = "unlocked",
            state_jammed = "jammed",
            device = BuildDeviceObject(door)
        };

        return JsonSerializer.Serialize(payload);
    }

    public string BuildSensorDiscoveryTopic(AccessPoint door) =>
        $"{_discoveryPrefix}/binary_sensor/codemaster_{door.Id}/config";

    public string BuildSensorDiscoveryPayload(AccessPoint door)
    {
        var topic = BuildSensorDiscoveryTopic(door);
        var payload = new
        {
            discovery_topic = topic,
            name = $"{door.Name} Sensor",
            unique_id = $"codemaster_{door.Id}_sensor",
            state_topic = $"{_statePrefix}/{door.Id}/sensor/state",
            device_class = "door",
            payload_on = "ON",
            payload_off = "OFF",
            device = BuildDeviceObject(door)
        };

        return JsonSerializer.Serialize(payload);
    }

    public string BuildAutoLockSwitchDiscoveryTopic(AccessPoint door) =>
        $"{_discoveryPrefix}/switch/codemaster_{door.Id}/config";

    public string BuildAutoLockSwitchDiscoveryPayload(AccessPoint door)
    {
        var topic = BuildAutoLockSwitchDiscoveryTopic(door);
        var payload = new
        {
            discovery_topic = topic,
            name = $"{door.Name} Auto-Lock",
            unique_id = $"codemaster_{door.Id}_autolock",
            state_topic = $"{_statePrefix}/{door.Id}/autolock/state",
            command_topic = $"{_statePrefix}/{door.Id}/autolock/set",
            payload_on = "ON",
            payload_off = "OFF",
            device = BuildDeviceObject(door)
        };

        return JsonSerializer.Serialize(payload);
    }

    public IReadOnlyList<HaDiscoveryMessage> BuildAllDiscoveryMessages(AccessPoint door)
    {
        return new List<HaDiscoveryMessage>
        {
            new(BuildEventDiscoveryTopic(door), BuildEventDiscoveryPayload(door)),
            new(BuildLockDiscoveryTopic(door), BuildLockDiscoveryPayload(door)),
            new(BuildSensorDiscoveryTopic(door), BuildSensorDiscoveryPayload(door)),
            new(BuildAutoLockSwitchDiscoveryTopic(door), BuildAutoLockSwitchDiscoveryPayload(door))
        };
    }

    private static object BuildDeviceObject(AccessPoint door)
    {
        return new
        {
            identifiers = new[] { $"codemaster_{door.Id}" },
            name = door.Name,
            manufacturer = "CodeMaster",
            model = "Access Controller"
        };
    }
}
