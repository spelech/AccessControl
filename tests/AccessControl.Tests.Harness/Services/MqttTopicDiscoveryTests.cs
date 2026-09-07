using AccessControl.Engine.Services;
using Xunit;

namespace AccessControl.Tests.Harness.Services;

public class MqttTopicDiscoveryTests
{
    [Fact]
    public void RecordTopic_RejectsNonContactMetrics_AndDiscoversContactSensors()
    {
        var svc = new MqttTopicDiscoveryService();

        // Feed realistic mixed homelab traffic
        svc.RecordTopic("zigbee2mqtt/front_door/battery", "{\"battery\":95}");
        svc.RecordTopic("zigbee2mqtt/front_door/linkquality", "{\"linkquality\":110}");
        svc.RecordTopic("zigbee2mqtt/front_door/temperature", "{\"temperature\":21.5}");
        svc.RecordTopic("homeassistant/sensor/washer_power/state", "1200");
        svc.RecordTopic("zigbee2mqtt/front_door_contact", "{\"contact\":true,\"battery\":95}");
        svc.RecordTopic("ring/keypad_v2/alarm/command", "{\"command\":\"disarm\"}");

        var sensors = svc.GetDiscoveredContactSensors();

        Assert.Single(sensors, s => s.Topic == "zigbee2mqtt/front_door_contact");
        Assert.DoesNotContain(sensors, s => s.Topic.Contains("battery"));
        Assert.DoesNotContain(sensors, s => s.Topic.Contains("linkquality"));
        Assert.DoesNotContain(sensors, s => s.Topic.Contains("temperature"));
        Assert.DoesNotContain(sensors, s => s.Topic.Contains("power"));
    }

    [Fact]
    public void RecordTopic_UpdatesExistingContactSensorState()
    {
        var svc = new MqttTopicDiscoveryService();
        var before = svc.GetDiscoveredContactSensors().First(s => s.Topic == "zigbee2mqtt/front_door_contact");
        Assert.Equal("closed", before.CurrentState);

        svc.RecordTopic("zigbee2mqtt/front_door_contact", "{\"contact\":false}");

        var after = svc.GetDiscoveredContactSensors().First(s => s.Topic == "zigbee2mqtt/front_door_contact");
        Assert.Equal("open", after.CurrentState);
    }

    [Fact]
    public void SniffActivity_DetectsRecentStateChanges_AfterGivenTimestamp()
    {
        var svc = new MqttTopicDiscoveryService();
        var t0 = DateTimeOffset.UtcNow;

        var initial = svc.SniffActivity(t0);
        Assert.False(initial.Detected);
        Assert.Null(initial.Event);

        // Simulate user physically opening the door
        svc.RecordTopic("zigbee2mqtt/patio_door_contact", "{\"contact\":false}");

        var detected = svc.SniffActivity(t0);
        Assert.True(detected.Detected);
        Assert.NotNull(detected.Event);
        Assert.Equal("zigbee2mqtt/patio_door_contact", detected.Event!.Topic);
        Assert.Equal("OPEN", detected.Event!.State);
        Assert.Equal("Patio Door Contact", detected.Event!.DeviceName);
    }

    [Fact]
    public void SniffActivity_ReturnsFalse_WhenNoEventsAfterGivenTimestamp()
    {
        var svc = new MqttTopicDiscoveryService();
        svc.RecordTopic("zigbee2mqtt/patio_door_contact", "{\"contact\":false}");

        var future = DateTimeOffset.UtcNow.AddMinutes(5);
        var result = svc.SniffActivity(future);

        Assert.False(result.Detected);
        Assert.Null(result.Event);
    }
}
