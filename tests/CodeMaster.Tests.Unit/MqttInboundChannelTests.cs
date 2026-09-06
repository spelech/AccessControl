using CodeMaster.Core.Models;
using CodeMaster.Engine.Channels;
using CodeMaster.Engine.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using NSubstitute;
using Xunit;

namespace CodeMaster.Tests.Unit;

public class MqttInboundChannelTests
{
    [Fact]
    public async Task Channel_PublishesAndConsumesMessages_InOrder()
    {
        var channel = new MqttInboundChannel(capacity: 100);

        var msg1 = new MqttInboundMessage("zwave/front_door", "{\"state\":\"locked\"}");
        var msg2 = new MqttInboundMessage("ring/keypad", "{\"command\":\"disarm\"}");
        var msg3 = new MqttInboundMessage("homeassistant/sensor", "{\"open\":true}");

        await channel.Writer.WriteAsync(msg1);
        await channel.Writer.WriteAsync(msg2);
        await channel.Writer.WriteAsync(msg3);

        var read1 = await channel.Reader.ReadAsync();
        var read2 = await channel.Reader.ReadAsync();
        var read3 = await channel.Reader.ReadAsync();

        Assert.Equal("zwave/front_door", read1.Topic);
        Assert.Equal("{\"state\":\"locked\"}", read1.Payload);

        Assert.Equal("ring/keypad", read2.Topic);
        Assert.Equal("{\"command\":\"disarm\"}", read2.Payload);

        Assert.Equal("homeassistant/sensor", read3.Topic);
        Assert.Equal("{\"open\":true}", read3.Payload);
    }

    [Fact]
    public async Task Channel_ReadAsync_PropagatesCancellationToken()
    {
        var channel = new MqttInboundChannel(capacity: 100);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await channel.Reader.ReadAsync(cts.Token);
        });
    }

    [Fact]
    public void MqttOptions_Defaults_AreConfiguredCorrectly()
    {
        var options = new MqttOptions();

        Assert.Equal("localhost", options.Host);
        Assert.Equal(1883, options.Port);
        Assert.Equal("codemaster", options.ClientId);
        Assert.Null(options.Username);
        Assert.Null(options.Password);
        Assert.False(options.UseTls);
        Assert.True(options.CleanSession);
        Assert.NotNull(options.SubscribedTopics);
        Assert.Contains("zwave/#", options.SubscribedTopics);
        Assert.Contains("ring/#", options.SubscribedTopics);
        Assert.Contains("zigbee2mqtt/#", options.SubscribedTopics);
        Assert.Contains("homeassistant/#", options.SubscribedTopics);
    }

    [Fact]
    public async Task MqttClientService_PublishAsync_ThrowsWhenNotConnected()
    {
        var channel = new MqttInboundChannel();
        var options = Options.Create(new MqttOptions());
        var mockMqttClient = Substitute.For<IMqttClient>();
        mockMqttClient.IsConnected.Returns(false);

        var service = new MqttClientService(options, channel, NullLogger<MqttClientService>.Instance, mockMqttClient);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.PublishAsync("test/topic", "payload");
        });
    }

    [Fact]
    public async Task MqttClientService_PublishAsync_PublishesWhenConnected()
    {
        var channel = new MqttInboundChannel();
        var options = Options.Create(new MqttOptions());
        var mockMqttClient = Substitute.For<IMqttClient>();
        mockMqttClient.IsConnected.Returns(true);

        var service = new MqttClientService(options, channel, NullLogger<MqttClientService>.Instance, mockMqttClient);

        await service.PublishAsync("test/topic", "payload", retain: true);

        await mockMqttClient.Received(1).PublishAsync(
            Arg.Is<MqttApplicationMessage>(m => m.Topic == "test/topic" && m.Retain == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MqttClientService_MessageReceived_WritesToInboundChannel()
    {
        var channel = new MqttInboundChannel();
        var options = Options.Create(new MqttOptions());
        var mockMqttClient = Substitute.For<IMqttClient>();

        var service = new MqttClientService(options, channel, NullLogger<MqttClientService>.Instance, mockMqttClient);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic("zwave/lock/event")
            .WithPayload("{\"action\":\"unlocked\"}")
            .Build();

        var args = new MqttApplicationMessageReceivedEventArgs(
            "client1",
            message,
            new MQTTnet.Packets.MqttPublishPacket(),
            (_, _) => Task.CompletedTask);

        await service.HandleIncomingMessageAsync(args);

        var received = await channel.Reader.ReadAsync();
        Assert.Equal("zwave/lock/event", received.Topic);
        Assert.Equal("{\"action\":\"unlocked\"}", received.Payload);
    }

    [Fact]
    public async Task MqttClientService_StartAsync_ConnectsAndSubscribes()
    {
        var channel = new MqttInboundChannel();
        var options = Options.Create(new MqttOptions
        {
            SubscribedTopics = ["zwave/#", "ring/#"]
        });

        var mockMqttClient = Substitute.For<IMqttClient>();
        var isConnected = false;
        mockMqttClient.IsConnected.Returns(_ => isConnected);

        mockMqttClient.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                isConnected = true;
                return Task.FromResult(new MqttClientConnectResult());
            });

        using var service = new MqttClientService(options, channel, NullLogger<MqttClientService>.Instance, mockMqttClient);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await service.StartAsync(cts.Token);

        // Give background loop a brief moment to connect and subscribe
        await Task.Delay(100);

        await mockMqttClient.Received().ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>());
        await mockMqttClient.Received().SubscribeAsync(
            Arg.Is<MqttClientSubscribeOptions>(o => o.TopicFilters.Count == 2),
            Arg.Any<CancellationToken>());

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MqttClientService_StopAsync_DisconnectsCleanly()
    {
        var channel = new MqttInboundChannel();
        var options = Options.Create(new MqttOptions());
        var mockMqttClient = Substitute.For<IMqttClient>();
        mockMqttClient.IsConnected.Returns(true);

        using var service = new MqttClientService(options, channel, NullLogger<MqttClientService>.Instance, mockMqttClient);

        await service.StopAsync(CancellationToken.None);

        await mockMqttClient.Received(1).DisconnectAsync(
            Arg.Is<MqttClientDisconnectOptions>(o => o.Reason == MqttClientDisconnectOptionsReason.NormalDisconnection),
            Arg.Any<CancellationToken>());
    }
}

