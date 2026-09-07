using System.Text.Json;
using AccessControl.Core.Models;
using AccessControl.Engine.Services;
using Xunit;

namespace AccessControl.Tests.Unit;

public class HaDiscoveryPayloadTests
{
    private readonly HomeAssistantDiscoveryService _service;
    private readonly AccessPoint _testDoor;

    public HaDiscoveryPayloadTests()
    {
        _service = new HomeAssistantDiscoveryService();
        _testDoor = new AccessPoint
        {
            Id = "side_door",
            Name = "Side Door"
        };
    }

    [Fact]
    public void BuildEventDiscoveryPayload_ReturnsValidJson_WithExpectedSchema()
    {
        var topic = _service.BuildEventDiscoveryTopic(_testDoor);
        var payload = _service.BuildEventDiscoveryPayload(_testDoor);

        Assert.Equal("homeassistant/event/accesscontrol_side_door/config", topic);
        Assert.Contains("homeassistant/event/accesscontrol_side_door/config", payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        Assert.Equal("Side Door Access", root.GetProperty("name").GetString());
        Assert.Equal("accesscontrol_side_door_access", root.GetProperty("unique_id").GetString());
        Assert.Equal("accesscontrol/side_door/event/state", root.GetProperty("state_topic").GetString());

        var eventTypes = root.GetProperty("event_types").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("keypad_unlock", eventTypes);
        Assert.Contains("manual_unlock", eventTypes);
        Assert.Contains("auto_lock", eventTypes);
        Assert.Contains("tamper", eventTypes);

        var device = root.GetProperty("device");
        var identifiers = device.GetProperty("identifiers").EnumerateArray().Select(i => i.GetString()).ToList();
        Assert.Contains("accesscontrol_side_door", identifiers);
        Assert.Equal("AccessControl", device.GetProperty("manufacturer").GetString());
        Assert.Equal("Access Controller", device.GetProperty("model").GetString());
    }

    [Fact]
    public void BuildLockDiscoveryPayload_ReturnsValidJson_WithExpectedSchema()
    {
        var topic = _service.BuildLockDiscoveryTopic(_testDoor);
        var payload = _service.BuildLockDiscoveryPayload(_testDoor);

        Assert.Equal("homeassistant/lock/accesscontrol_side_door/config", topic);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        Assert.Equal("Side Door", root.GetProperty("name").GetString());
        Assert.Equal("accesscontrol_side_door_lock", root.GetProperty("unique_id").GetString());
        Assert.Equal("accesscontrol/side_door/lock/state", root.GetProperty("state_topic").GetString());
        Assert.Equal("accesscontrol/side_door/lock/set", root.GetProperty("command_topic").GetString());
        Assert.Equal("LOCK", root.GetProperty("payload_lock").GetString());
        Assert.Equal("UNLOCK", root.GetProperty("payload_unlock").GetString());
        Assert.Equal("locked", root.GetProperty("state_locked").GetString());
        Assert.Equal("unlocked", root.GetProperty("state_unlocked").GetString());
        Assert.Equal("jammed", root.GetProperty("state_jammed").GetString());

        var device = root.GetProperty("device");
        var identifiers = device.GetProperty("identifiers").EnumerateArray().Select(i => i.GetString()).ToList();
        Assert.Contains("accesscontrol_side_door", identifiers);
    }

    [Fact]
    public void BuildSensorDiscoveryPayload_ReturnsValidJson_WithDoorDeviceClass()
    {
        var topic = _service.BuildSensorDiscoveryTopic(_testDoor);
        var payload = _service.BuildSensorDiscoveryPayload(_testDoor);

        Assert.Equal("homeassistant/binary_sensor/accesscontrol_side_door/config", topic);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        Assert.Equal("Side Door Door Sensor", root.GetProperty("name").GetString());
        Assert.Equal("accesscontrol_side_door_sensor", root.GetProperty("unique_id").GetString());
        Assert.Equal("accesscontrol/side_door/sensor/state", root.GetProperty("state_topic").GetString());
        Assert.Equal("door", root.GetProperty("device_class").GetString());
        Assert.Equal("open", root.GetProperty("payload_on").GetString());
        Assert.Equal("closed", root.GetProperty("payload_off").GetString());
    }

    [Fact]
    public void BuildAutoLockSwitchDiscoveryPayload_ReturnsValidJson_WithSwitchSchema()
    {
        var topic = _service.BuildAutoLockSwitchDiscoveryTopic(_testDoor);
        var payload = _service.BuildAutoLockSwitchDiscoveryPayload(_testDoor);

        Assert.Equal("homeassistant/switch/accesscontrol_side_door/config", topic);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        Assert.Equal("Side Door Auto-Lock", root.GetProperty("name").GetString());
        Assert.Equal("accesscontrol_side_door_autolock", root.GetProperty("unique_id").GetString());
        Assert.Equal("accesscontrol/side_door/autolock/state", root.GetProperty("state_topic").GetString());
        Assert.Equal("accesscontrol/side_door/autolock/set", root.GetProperty("command_topic").GetString());
    }

    [Fact]
    public void BuildAllDiscoveryMessages_ReturnsAllFourEntities()
    {
        var messages = _service.BuildAllDiscoveryMessages(_testDoor);

        Assert.Equal(4, messages.Count);
        Assert.Contains(messages, m => m.Topic.StartsWith("homeassistant/event/"));
        Assert.Contains(messages, m => m.Topic.StartsWith("homeassistant/lock/"));
        Assert.Contains(messages, m => m.Topic.StartsWith("homeassistant/binary_sensor/"));
        Assert.Contains(messages, m => m.Topic.StartsWith("homeassistant/switch/"));
    }
}
