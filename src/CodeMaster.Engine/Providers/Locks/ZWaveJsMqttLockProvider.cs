using System.Collections.Concurrent;
using System.Text.Json;
using CodeMaster.Core.DTOs;
using CodeMaster.Core.Interfaces;
using CodeMaster.Core.Models;
using CodeMaster.Engine.Mqtt;

namespace CodeMaster.Engine.Providers.Locks;

public class ZWaveJsMqttLockProvider : ILockProvider
{
    private readonly string _nodeId;
    private readonly int _endpoint;
    private readonly IMqttClientService? _mqttClient;
    private readonly ConcurrentDictionary<int, HardwareSlotDto> _slots = new();
    private LockState _currentState = LockState.Unknown;

    public LockCapabilities Capabilities =>
        LockCapabilities.SupportsHardwareSlots |
        LockCapabilities.SupportsRemoteLock |
        LockCapabilities.SupportsRemoteUnlock |
        LockCapabilities.SupportsJammedReport;

    public string NodeId => _nodeId;
    public int Endpoint => _endpoint;

    public ZWaveJsMqttLockProvider(string nodeId = "front_door", int endpoint = 0, IMqttClientService? mqttClient = null)
    {
        _nodeId = NormalizeNodeId(nodeId);
        _endpoint = endpoint;
        _mqttClient = mqttClient;
    }

    private static string NormalizeNodeId(string nodeId)
    {
        var trimmed = nodeId.Trim().Trim('/');
        if (trimmed.StartsWith("zwave/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["zwave/".Length..];
        }
        return string.IsNullOrWhiteSpace(trimmed) ? "front_door" : trimmed;
    }

    public static string GetLockTopic(string nodeId, int endpoint = 0) =>
        $"zwave/{NormalizeNodeId(nodeId)}/door_lock/endpoint_{endpoint}/targetState/set";

    public string GetLockTopic() => GetLockTopic(_nodeId, _endpoint);

    public static string GetUnlockTopic(string nodeId, int endpoint = 0) =>
        $"zwave/{NormalizeNodeId(nodeId)}/door_lock/endpoint_{endpoint}/targetState/set";

    public string GetUnlockTopic() => GetUnlockTopic(_nodeId, _endpoint);

    public static string GetSetSlotCodeTopic(string nodeId, int endpoint = 0, int slotNumber = 0) =>
        $"zwave/{NormalizeNodeId(nodeId)}/user_code/endpoint_{endpoint}/set";

    public string GetSetSlotCodeTopic(int slotNumber = 0) => GetSetSlotCodeTopic(_nodeId, _endpoint, slotNumber);

    public static string GetClearSlotCodeTopic(string nodeId, int endpoint = 0, int slotNumber = 0) =>
        $"zwave/{NormalizeNodeId(nodeId)}/user_code/endpoint_{endpoint}/set";

    public string GetClearSlotCodeTopic(int slotNumber = 0) => GetClearSlotCodeTopic(_nodeId, _endpoint, slotNumber);

    public Task<LockState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentState);
    }

    public async Task<bool> LockAsync(CancellationToken cancellationToken = default)
    {
        var topic = GetLockTopic();
        if (_mqttClient != null)
        {
            await _mqttClient.PublishAsync(topic, "true", retain: false, cancellationToken);
        }

        _currentState = LockState.Locked;
        return true;
    }

    public async Task<bool> UnlockAsync(CancellationToken cancellationToken = default)
    {
        var topic = GetUnlockTopic();
        if (_mqttClient != null)
        {
            await _mqttClient.PublishAsync(topic, "false", retain: false, cancellationToken);
        }

        _currentState = LockState.Unlocked;
        return true;
    }

    public Task<IReadOnlyList<HardwareSlotDto>> GetSlotCodesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<HardwareSlotDto> list = _slots.Values.OrderBy(s => s.SlotNumber).ToList();
        return Task.FromResult(list);
    }

    public async Task<bool> SetSlotCodeAsync(int slotNumber, string pinCode, string? label = null, CancellationToken cancellationToken = default)
    {
        var topic = GetSetSlotCodeTopic(slotNumber);
        var payloadObj = new
        {
            slot = slotNumber,
            usercode = pinCode,
            code = pinCode,
            label
        };
        var payload = JsonSerializer.Serialize(payloadObj);

        if (_mqttClient != null)
        {
            await _mqttClient.PublishAsync(topic, payload, retain: false, cancellationToken);
        }

        _slots[slotNumber] = new HardwareSlotDto(slotNumber, true, pinCode, label);
        return true;
    }

    public async Task<bool> ClearSlotCodeAsync(int slotNumber, CancellationToken cancellationToken = default)
    {
        var topic = GetClearSlotCodeTopic(slotNumber);
        var payloadObj = new
        {
            slot = slotNumber,
            usercode = string.Empty,
            code = string.Empty
        };
        var payload = JsonSerializer.Serialize(payloadObj);

        if (_mqttClient != null)
        {
            await _mqttClient.PublishAsync(topic, payload, retain: false, cancellationToken);
        }

        _slots.TryRemove(slotNumber, out _);
        return true;
    }

    public bool TryUpdateFromMessage(MqttInboundMessage message)
    {
        var expectedPrefix = $"zwave/{_nodeId}/door_lock/endpoint_{_endpoint}/";
        if (message.Topic.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var subTopic = message.Topic[expectedPrefix.Length..];
            if (subTopic.StartsWith("currentState", StringComparison.OrdinalIgnoreCase) ||
                subTopic.StartsWith("currentMode", StringComparison.OrdinalIgnoreCase) ||
                subTopic.StartsWith("value", StringComparison.OrdinalIgnoreCase))
            {
                var payload = message.Payload.Trim().Trim('"');
                if (bool.TryParse(payload, out var isLocked))
                {
                    _currentState = isLocked ? LockState.Locked : LockState.Unlocked;
                    return true;
                }

                if (payload.Equals("locked", StringComparison.OrdinalIgnoreCase) ||
                    payload.Equals("255", StringComparison.OrdinalIgnoreCase))
                {
                    _currentState = LockState.Locked;
                    return true;
                }

                if (payload.Equals("unlocked", StringComparison.OrdinalIgnoreCase) ||
                    payload.Equals("0", StringComparison.OrdinalIgnoreCase))
                {
                    _currentState = LockState.Unlocked;
                    return true;
                }

                if (payload.Equals("jammed", StringComparison.OrdinalIgnoreCase))
                {
                    _currentState = LockState.Jammed;
                    return true;
                }
            }
        }

        return false;
    }
}
