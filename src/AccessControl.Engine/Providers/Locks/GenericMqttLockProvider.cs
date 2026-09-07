using AccessControl.Core.DTOs;
using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;
using AccessControl.Engine.Mqtt;

namespace AccessControl.Engine.Providers.Locks;

public class GenericMqttLockProvider : ILockProvider
{
    private readonly string _commandTopic;
    private readonly string _stateTopic;
    private readonly string _lockPayload;
    private readonly string _unlockPayload;
    private readonly IMqttClientService? _mqttClient;
    private LockState _currentState = LockState.Unknown;

    public LockCapabilities Capabilities =>
        LockCapabilities.SupportsRemoteLock |
        LockCapabilities.SupportsRemoteUnlock;

    public string CommandTopic => _commandTopic;
    public string StateTopic => _stateTopic;
    public string LockPayload => _lockPayload;
    public string UnlockPayload => _unlockPayload;

    public GenericMqttLockProvider(
        string commandTopic,
        string stateTopic,
        string lockPayload = "LOCK",
        string unlockPayload = "UNLOCK",
        IMqttClientService? mqttClient = null)
    {
        _commandTopic = commandTopic;
        _stateTopic = stateTopic;
        _lockPayload = lockPayload;
        _unlockPayload = unlockPayload;
        _mqttClient = mqttClient;
    }

    public Task<LockState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentState);
    }

    public async Task<bool> LockAsync(CancellationToken cancellationToken = default)
    {
        if (_mqttClient != null)
        {
            try
            {
                await _mqttClient.PublishAsync(_commandTopic, _lockPayload, retain: false, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Offline fallback
            }
        }

        _currentState = LockState.Locked;
        return true;
    }

    public async Task<bool> UnlockAsync(CancellationToken cancellationToken = default)
    {
        if (_mqttClient != null)
        {
            try
            {
                await _mqttClient.PublishAsync(_commandTopic, _unlockPayload, retain: false, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Offline fallback
            }
        }

        _currentState = LockState.Unlocked;
        return true;
    }

    public Task<IReadOnlyList<HardwareSlotDto>> GetSlotCodesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<HardwareSlotDto>>(Array.Empty<HardwareSlotDto>());
    }

    public Task<bool> SetSlotCodeAsync(int slotNumber, string pinCode, string? label = null, CancellationToken cancellationToken = default)
    {
        // Generic MQTT relay / deadbolt does not support hardware user codes
        return Task.FromResult(false);
    }

    public Task<bool> ClearSlotCodeAsync(int slotNumber, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public bool TryUpdateFromMessage(MqttInboundMessage message)
    {
        if (string.Equals(message.Topic, _stateTopic, StringComparison.OrdinalIgnoreCase))
        {
            var payload = message.Payload.Trim().Trim('"');
            if (string.Equals(payload, _lockPayload, StringComparison.OrdinalIgnoreCase))
            {
                _currentState = LockState.Locked;
                return true;
            }
            if (string.Equals(payload, _unlockPayload, StringComparison.OrdinalIgnoreCase))
            {
                _currentState = LockState.Unlocked;
                return true;
            }
        }

        return false;
    }
}
