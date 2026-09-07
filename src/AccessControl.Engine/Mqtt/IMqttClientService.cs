using Microsoft.Extensions.Hosting;

namespace AccessControl.Engine.Mqtt;

/// <summary>
/// Service managing resilient MQTT connection, subscriptions, and message publishing.
/// </summary>
public interface IMqttClientService : IHostedService
{
    /// <summary>
    /// Gets a value indicating whether the MQTT client is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Publishes a message to the specified MQTT topic.
    /// </summary>
    /// <param name="topic">Destination MQTT topic.</param>
    /// <param name="payload">Payload string to publish.</param>
    /// <param name="retain">Whether the broker should retain the message.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishAsync(string topic, string payload, bool retain = false, CancellationToken ct = default);
}
