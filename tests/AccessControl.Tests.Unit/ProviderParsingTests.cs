using AccessControl.Core.DTOs;
using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;
using AccessControl.Engine.Mqtt;
using AccessControl.Engine.Providers.Keypads;
using AccessControl.Engine.Providers.Locks;
using AccessControl.Engine.Providers.Sensors;
using NSubstitute;
using Xunit;

namespace AccessControl.Tests.Unit;

public class ProviderParsingTests
{
    [Fact]
    public void RingKeypad_ParsesDisarmCommandWithPin()
    {
        var provider = new RingMqttKeypadProvider();
        var payload = "{\"command\":\"disarm\",\"code\":\"4821\"}";
        var msg = new MqttInboundMessage("ring/loc/alarm/command", payload);

        var success = provider.TryParseKeypadEvent(msg, out var evt);

        Assert.True(success);
        Assert.NotNull(evt);
        Assert.Equal("4821", evt!.Pin);
        Assert.Equal(KeypadAction.Disarm, evt.Action);
    }

    [Fact]
    public void RingKeypad_ParsesArmAwayAndArmStayCommands()
    {
        var provider = new RingMqttKeypadProvider();

        var awayMsg = new MqttInboundMessage("ring/loc/alarm/command", "{\"command\":\"arm_away\",\"code\":\"1234\"}");
        Assert.True(provider.TryParseKeypadEvent(awayMsg, out var awayEvt));
        Assert.Equal(KeypadAction.ArmAway, awayEvt!.Action);
        Assert.Equal("1234", awayEvt.Pin);

        var stayMsg = new MqttInboundMessage("ring/loc/alarm/command", "{\"command\":\"arm_stay\",\"code\":\"5678\"}");
        Assert.True(provider.TryParseKeypadEvent(stayMsg, out var stayEvt));
        Assert.Equal(KeypadAction.ArmStay, stayEvt!.Action);
        Assert.Equal("5678", stayEvt.Pin);
    }

    [Fact]
    public void RingKeypad_IgnoresInvalidPayloadOrUnrelatedTopic()
    {
        var provider = new RingMqttKeypadProvider();

        var invalidJsonMsg = new MqttInboundMessage("ring/loc/alarm/command", "not-json");
        Assert.False(provider.TryParseKeypadEvent(invalidJsonMsg, out _));

        var missingCmdMsg = new MqttInboundMessage("ring/loc/alarm/command", "{\"other\":\"prop\"}");
        Assert.False(provider.TryParseKeypadEvent(missingCmdMsg, out _));
    }

    [Fact]
    public void BuiltInLockKeypadProvider_ParsesZWaveSlotUnlockNotification()
    {
        var provider = new BuiltInLockKeypadProvider();
        var topic = "zwave/front_door/alarm/endpoint_0/notification";
        var payload = "{\"type\":19,\"slot\":3}";
        var msg = new MqttInboundMessage(topic, payload);

        var success = provider.TryParseKeypadEvent(msg, out var evt);

        Assert.True(success);
        Assert.NotNull(evt);
        Assert.Equal(KeypadAction.Unlock, evt!.Action);
        Assert.Equal(3, evt.SlotNumber);
        Assert.Equal(KeypadMode.HardwareSlotted, provider.Mode);
    }

    [Fact]
    public void BuiltInLockKeypadProvider_ParsesAlternativeZWaveAccessControlPayload()
    {
        var provider = new BuiltInLockKeypadProvider();
        var topic = "zwave/front_door/notification";
        var payload = "{\"alarm_type\":19,\"userId\":5}";
        var msg = new MqttInboundMessage(topic, payload);

        var success = provider.TryParseKeypadEvent(msg, out var evt);

        Assert.True(success);
        Assert.NotNull(evt);
        Assert.Equal(KeypadAction.Unlock, evt!.Action);
        Assert.Equal(5, evt.SlotNumber);
    }

    [Theory]
    [InlineData("ON", DoorContactState.Open)]
    [InlineData("OFF", DoorContactState.Closed)]
    public void MqttContactSensorProvider_ParsesOnOffPayloads(string payload, DoorContactState expectedState)
    {
        var provider = new MqttContactSensorProvider(new MqttContactSensorOptions
        {
            OpenPayload = "ON",
            ClosedPayload = "OFF"
        });

        var msg = new MqttInboundMessage("sensor/door/state", payload);
        var success = provider.TryParseContactEvent(msg, out var state);

        Assert.True(success);
        Assert.Equal(expectedState, state);
    }

    [Theory]
    [InlineData("open", DoorContactState.Open)]
    [InlineData("closed", DoorContactState.Closed)]
    public void MqttContactSensorProvider_ParsesOpenClosedPayloads(string payload, DoorContactState expectedState)
    {
        var provider = new MqttContactSensorProvider(new MqttContactSensorOptions
        {
            OpenPayload = "open",
            ClosedPayload = "closed"
        });

        var msg = new MqttInboundMessage("sensor/door/state", payload);
        var success = provider.TryParseContactEvent(msg, out var state);

        Assert.True(success);
        Assert.Equal(expectedState, state);
    }

    [Theory]
    [InlineData("open", DoorContactState.Closed)]
    [InlineData("closed", DoorContactState.Open)]
    public void MqttContactSensorProvider_RespectsInvertConfiguration(string payload, DoorContactState expectedState)
    {
        var provider = new MqttContactSensorProvider(new MqttContactSensorOptions
        {
            OpenPayload = "open",
            ClosedPayload = "closed",
            Invert = true
        });

        var msg = new MqttInboundMessage("sensor/door/state", payload);
        var success = provider.TryParseContactEvent(msg, out var state);

        Assert.True(success);
        Assert.Equal(expectedState, state);
    }

    [Fact]
    public void ZWaveJsMqttLockProvider_CommandTopicGeneration_IsAccurate()
    {
        var provider = new ZWaveJsMqttLockProvider(nodeId: "front_door");

        Assert.Equal("zwave/front_door/door_lock/endpoint_0/targetState/set", provider.GetLockTopic());
        Assert.Equal("zwave/front_door/door_lock/endpoint_0/targetState/set", provider.GetUnlockTopic());
        Assert.Equal("zwave/front_door/user_code/endpoint_0/set", provider.GetSetSlotCodeTopic(slotNumber: 3));
        Assert.Equal("zwave/front_door/user_code/endpoint_0/set", provider.GetClearSlotCodeTopic(slotNumber: 3));
    }

    [Fact]
    public async Task ZWaveJsMqttLockProvider_ExecutesLockAndUnlockCommands()
    {
        var mqtt = Substitute.For<IMqttClientService>();
        var provider = new ZWaveJsMqttLockProvider(nodeId: "front_door", mqttClient: mqtt);

        var lockSuccess = await provider.LockAsync();
        Assert.True(lockSuccess);
        await mqtt.Received(1).PublishAsync("zwave/front_door/door_lock/endpoint_0/targetState/set", "true", false, Arg.Any<CancellationToken>());

        var unlockSuccess = await provider.UnlockAsync();
        Assert.True(unlockSuccess);
        await mqtt.Received(1).PublishAsync("zwave/front_door/door_lock/endpoint_0/targetState/set", "false", false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ZWaveJsMqttLockProvider_ExecutesSlotSetAndClearCommands()
    {
        var mqtt = Substitute.For<IMqttClientService>();
        var provider = new ZWaveJsMqttLockProvider(nodeId: "front_door", mqttClient: mqtt);

        var setSuccess = await provider.SetSlotCodeAsync(2, "9988", "Guest");
        Assert.True(setSuccess);
        await mqtt.Received(1).PublishAsync(
            "zwave/front_door/user_code/endpoint_0/set",
            Arg.Is<string>(s => s.Contains("\"slot\":2") && s.Contains("9988")),
            false,
            Arg.Any<CancellationToken>());

        var clearSuccess = await provider.ClearSlotCodeAsync(2);
        Assert.True(clearSuccess);
        await mqtt.Received(1).PublishAsync(
            "zwave/front_door/user_code/endpoint_0/set",
            Arg.Is<string>(s => s.Contains("\"slot\":2") && s.Contains("\"usercode\":\"\"")),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenericMqttLockProvider_PublishesConfiguredCommands()
    {
        var mqtt = Substitute.For<IMqttClientService>();
        var provider = new GenericMqttLockProvider(
            commandTopic: "cmnd/tasmota_lock/POWER",
            stateTopic: "stat/tasmota_lock/POWER",
            lockPayload: "ON",
            unlockPayload: "OFF",
            mqttClient: mqtt);

        Assert.Equal(LockCapabilities.RemoteControl, provider.Capabilities & LockCapabilities.RemoteControl);

        await provider.LockAsync();
        await mqtt.Received(1).PublishAsync("cmnd/tasmota_lock/POWER", "ON", false, Arg.Any<CancellationToken>());

        await provider.UnlockAsync();
        await mqtt.Received(1).PublishAsync("cmnd/tasmota_lock/POWER", "OFF", false, Arg.Any<CancellationToken>());
    }
}
