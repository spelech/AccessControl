namespace AccessControl.Core.Models;

public record MqttInboundMessage(string Topic, string Payload, DateTimeOffset? Timestamp = null);
