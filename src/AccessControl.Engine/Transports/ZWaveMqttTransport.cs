using System.Collections.Concurrent;
using System.Text.Json;
using AccessControl.Core.DTOs;
using AccessControl.Core.Models;
using AccessControl.Core.Transports;
using AccessControl.Engine.Mqtt;
using Microsoft.Extensions.Logging;

namespace AccessControl.Engine.Transports;

public class ZWaveMqttTransport : ILockTransport, IKeypadTransport
{
    private readonly IMqttClientService? _mqttClient;
    private readonly string _prefix;
    private readonly ILogger<ZWaveMqttTransport> _logger;
    private readonly ConcurrentDictionary<string, LockState> _lockStates = new(StringComparer.OrdinalIgnoreCase);

    public string TransportId => "zwave_mqtt";
    public string DisplayName => "Z-Wave MQTT (Mosquitto)";
    public bool IsConnected => _mqttClient?.IsConnected ?? false;
    public TransportStatus Status => IsConnected ? TransportStatus.Connected : TransportStatus.Disconnected;

    public event Action<TransportStatusChangedEventArgs>? OnStatusChanged = delegate { };
    public event Action<LockStateUpdatedEventArgs>? OnLockStateChanged;
    public event Action<KeypadEntryEventArgs>? OnKeypadEntry = delegate { };

    public ZWaveMqttTransport(
        IMqttClientService? mqttClient,
        string prefix,
        ILogger<ZWaveMqttTransport> logger)
    {
        _mqttClient = mqttClient;
        _prefix = string.IsNullOrWhiteSpace(prefix) ? "zwave" : prefix.Trim('/');
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Z-Wave MQTT Transport started with prefix '{Prefix}'", _prefix);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Z-Wave MQTT Transport stopped.");
        return Task.CompletedTask;
    }

    public async Task<bool> SetLockStateAsync(string deviceTarget, bool locked, CancellationToken ct = default)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected)
        {
            _logger.LogWarning("Cannot set lock state: MQTT client not connected.");
            return false;
        }

        var normalizedTarget = NormalizeTarget(deviceTarget);
        var topic = $"{_prefix}/{normalizedTarget}/door_lock/endpoint_0/targetState/set";
        var payload = JsonSerializer.Serialize(new { value = locked });

        try
        {
            await _mqttClient.PublishAsync(topic, payload, retain: false, ct: ct);
            var state = locked ? LockState.Locked : LockState.Unlocked;
            _lockStates[normalizedTarget] = state;
            OnLockStateChanged?.Invoke(new LockStateUpdatedEventArgs(deviceTarget, state, "ZWaveMqtt"));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish lock command to topic {Topic}", topic);
            return false;
        }
    }

    public Task<LockState> GetLockStateAsync(string deviceTarget, CancellationToken ct = default)
    {
        var normalized = NormalizeTarget(deviceTarget);
        if (_lockStates.TryGetValue(normalized, out var state))
        {
            return Task.FromResult(state);
        }

        return Task.FromResult(LockState.Unknown);
    }

    public async Task<bool> SetUserCodeAsync(string deviceTarget, int slot, string pin, string? label, CancellationToken ct = default)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected)
        {
            return false;
        }

        var normalizedTarget = NormalizeTarget(deviceTarget);
        var topic = $"{_prefix}/{normalizedTarget}/user_code/endpoint_0/set";
        var payload = JsonSerializer.Serialize(new { value = pin });

        try
        {
            await _mqttClient.PublishAsync(topic, payload, retain: false, ct: ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish set user code to topic {Topic}", topic);
            return false;
        }
    }

    public async Task<bool> ClearUserCodeAsync(string deviceTarget, int slot, CancellationToken ct = default)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected)
        {
            return false;
        }

        var normalizedTarget = NormalizeTarget(deviceTarget);
        var topic = $"{_prefix}/{normalizedTarget}/user_code/endpoint_0/set";
        var payload = JsonSerializer.Serialize(new { value = "" });

        try
        {
            await _mqttClient.PublishAsync(topic, payload, retain: false, ct: ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish clear user code to topic {Topic}", topic);
            return false;
        }
    }

    public Task<IReadOnlyList<HardwareSlotDto>> GetUserCodesAsync(string deviceTarget, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<HardwareSlotDto>>(Array.Empty<HardwareSlotDto>());
    }

    public Task<bool> SetKeypadModeAsync(string deviceTarget, KeypadArmMode mode, CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    private string NormalizeTarget(string target)
    {
        var trimmed = target.Trim().Trim('/');
        if (trimmed.StartsWith($"{_prefix}/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[(_prefix.Length + 1)..];
        }
        return string.IsNullOrWhiteSpace(trimmed) ? "front_door" : trimmed;
    }
}
