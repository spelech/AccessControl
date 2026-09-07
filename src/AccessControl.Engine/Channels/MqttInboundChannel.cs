using System.Threading.Channels;
using AccessControl.Core.Models;

namespace AccessControl.Engine.Channels;

/// <summary>
/// Thread-safe bounded channel wrapper for inbound MQTT messages.
/// </summary>
public sealed class MqttInboundChannel : IMqttInboundChannel
{
    public const int DefaultCapacity = 5000;

    private readonly Channel<MqttInboundMessage> _channel;

    public MqttInboundChannel(int capacity = DefaultCapacity)
    {
        var options = new BoundedChannelOptions(capacity <= 0 ? DefaultCapacity : capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false
        };

        _channel = Channel.CreateBounded<MqttInboundMessage>(options);
    }

    /// <inheritdoc />
    public ChannelWriter<MqttInboundMessage> Writer => _channel.Writer;

    /// <inheritdoc />
    public ChannelReader<MqttInboundMessage> Reader => _channel.Reader;
}
