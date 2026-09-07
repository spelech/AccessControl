using System.Threading.Channels;
using AccessControl.Core.Models;

namespace AccessControl.Engine.Channels;

/// <summary>
/// Defines an inbound channel for ingesting raw MQTT messages into CodeMaster engine.
/// </summary>
public interface IMqttInboundChannel
{
    /// <summary>
    /// Channel writer for publishing received MQTT messages.
    /// </summary>
    ChannelWriter<MqttInboundMessage> Writer { get; }

    /// <summary>
    /// Channel reader for consuming queued MQTT messages.
    /// </summary>
    ChannelReader<MqttInboundMessage> Reader { get; }
}
