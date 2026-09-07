using AccessControl.Core.DTOs;
using AccessControl.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccessControl.Engine.Services;

public class SystemSettingsService
{
    private readonly ISettingsRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemSettingsService> _logger;

    public SystemSettingsService(
        ISettingsRepository repository,
        IConfiguration configuration,
        ILogger<SystemSettingsService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SystemSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var dbSettings = await _repository.GetAllSettingsAsync(ct);

        string GetValue(string key, string envVar, string configKey, string defaultValue)
        {
            if (dbSettings.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val;
            }

            var env = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env;
            }

            var cfg = _configuration[configKey];
            if (!string.IsNullOrWhiteSpace(cfg))
            {
                return cfg;
            }

            return defaultValue;
        }

        int GetInt(string key, string envVar, string configKey, int defaultValue)
        {
            var strVal = GetValue(key, envVar, configKey, defaultValue.ToString());
            return int.TryParse(strVal, out var parsed) ? parsed : defaultValue;
        }

        return new SystemSettingsDto
        {
            ZWaveTransportType = GetValue("zwave.transport_type", "ZWAVE_TRANSPORT", "ZWave:Transport", "WebSocket"),
            ZWaveWebSocketUrl = GetValue("zwave.websocket_url", "ZWAVE_WS_URL", "ZWave:WebSocketUrl", "ws://10.0.0.10:8106"),
            ZWaveMqttPrefix = GetValue("zwave.mqtt_prefix", "ZWAVE_MQTT_PREFIX", "ZWave:MqttPrefix", "zwave"),
            MqttHost = GetValue("mqtt.host", "MQTT_HOST", "Mqtt:Host", "10.0.0.10"),
            MqttPort = GetInt("mqtt.port", "MQTT_PORT", "Mqtt:Port", 8100),
            MqttUsername = GetValue("mqtt.username", "MQTT_USER", "Mqtt:Username", ""),
            MqttPassword = GetValue("mqtt.password", "MQTT_PASSWORD", "Mqtt:Password", ""),
            AppriseUrl = GetValue("apprise.url", "APPRISE_URL", "Apprise:Url", "")
        };
    }

    public async Task SaveSettingsAsync(SystemSettingsDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var dict = new Dictionary<string, string>
        {
            ["zwave.transport_type"] = dto.ZWaveTransportType,
            ["zwave.websocket_url"] = dto.ZWaveWebSocketUrl,
            ["zwave.mqtt_prefix"] = dto.ZWaveMqttPrefix,
            ["mqtt.host"] = dto.MqttHost,
            ["mqtt.port"] = dto.MqttPort.ToString(),
            ["mqtt.username"] = dto.MqttUsername,
            ["mqtt.password"] = dto.MqttPassword,
            ["apprise.url"] = dto.AppriseUrl
        };

        await _repository.SetSettingsAsync(dict, ct);
        _logger.LogInformation("System settings saved successfully to database.");
    }
}
