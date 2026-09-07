namespace AccessControl.Core.DTOs;

public record SystemSettingsDto
{
    public string ZWaveTransportType { get; init; } = "WebSocket";
    public string ZWaveWebSocketUrl { get; init; } = "ws://10.0.0.10:8106";
    public string ZWaveMqttPrefix { get; init; } = "zwave";
    public string MqttHost { get; init; } = "10.0.0.10";
    public int MqttPort { get; init; } = 8100;
    public string MqttUsername { get; init; } = "";
    public string MqttPassword { get; init; } = "";
    public string AppriseUrl { get; init; } = "";
}
