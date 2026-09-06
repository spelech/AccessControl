using CodeMaster.Core.Models;
using CodeMaster.Core.Transports;
using CodeMaster.Engine.Mqtt;
using CodeMaster.Engine.Transports;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodeMaster.Tests.Unit;

public class ZWaveMqttTransportTests
{
    private class MockMqttClient : IMqttClientService
    {
        public bool IsConnected => true;
        public List<(string Topic, string Payload)> PublishedMessages { get; } = new();

        public event Action<MqttInboundMessage>? MessageReceived = delegate { };

        public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task PublishAsync(string topic, string payload, bool retain = false, CancellationToken ct = default)
        {
            PublishedMessages.Add((topic, payload));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SetLockStateAsync_PublishesCorrectZWaveTopicAndPayload()
    {
        var mockMqtt = new MockMqttClient();
        var transport = new ZWaveMqttTransport(mockMqtt, "zwave", NullLogger<ZWaveMqttTransport>.Instance);

        LockStateUpdatedEventArgs? receivedEvent = null;
        transport.OnLockStateChanged += e => receivedEvent = e;

        var success = await transport.SetLockStateAsync("front_door", true);

        Assert.True(success);
        Assert.Single(mockMqtt.PublishedMessages);
        Assert.Equal("zwave/front_door/door_lock/endpoint_0/targetState/set", mockMqtt.PublishedMessages[0].Topic);
        Assert.Contains("\"value\":true", mockMqtt.PublishedMessages[0].Payload);

        Assert.NotNull(receivedEvent);
        Assert.Equal(LockState.Locked, receivedEvent.State);
    }

    [Fact]
    public async Task TransportLockProviderAdapter_DelegatesToTransport()
    {
        var mockMqtt = new MockMqttClient();
        var transport = new ZWaveMqttTransport(mockMqtt, "zwave", NullLogger<ZWaveMqttTransport>.Instance);
        var adapter = new TransportLockProviderAdapter(transport, "front_door");

        var lockSuccess = await adapter.LockAsync();
        Assert.True(lockSuccess);

        var unlockSuccess = await adapter.UnlockAsync();
        Assert.True(unlockSuccess);

        Assert.Equal(2, mockMqtt.PublishedMessages.Count);
        Assert.Contains("\"value\":false", mockMqtt.PublishedMessages[1].Payload);
    }
}
