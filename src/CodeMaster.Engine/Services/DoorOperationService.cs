using System.Collections.Concurrent;
using System.Text.Json;
using CodeMaster.Core.Interfaces;
using CodeMaster.Core.Models;
using CodeMaster.Data.Repositories;
using CodeMaster.Engine.Mqtt;
using CodeMaster.Engine.Providers.Locks;
using Microsoft.Extensions.Logging;

namespace CodeMaster.Engine.Services;

public class DoorOperationService : IDoorOperationService
{
    private readonly IAccessPointRepository _doorRepo;
    private readonly IHardwareSlotRepository? _slotRepo;
    private readonly IMqttClientService? _mqttClient;
    private readonly ILogger<DoorOperationService> _logger;

    private static readonly ConcurrentDictionary<string, LockState> _lockStates = new();
    private static readonly ConcurrentDictionary<string, DoorContactState> _contactStates = new();
    private static readonly ConcurrentDictionary<string, AutoLockStateMachine> _autoLockStateMachines = new();

    public DoorOperationService(
        IAccessPointRepository doorRepo,
        ILogger<DoorOperationService> logger,
        IMqttClientService? mqttClient = null,
        IHardwareSlotRepository? slotRepo = null)
    {
        _doorRepo = doorRepo;
        _logger = logger;
        _mqttClient = mqttClient;
        _slotRepo = slotRepo;
    }

    public Task<LockState> GetDoorLockStateAsync(string doorId, CancellationToken ct = default)
    {
        if (_lockStates.TryGetValue(doorId, out var state))
        {
            return Task.FromResult(state);
        }

        return Task.FromResult(LockState.Locked);
    }

    public Task<DoorContactState> GetDoorContactStateAsync(string doorId, CancellationToken ct = default)
    {
        if (_contactStates.TryGetValue(doorId, out var state))
        {
            return Task.FromResult(state);
        }

        return Task.FromResult(DoorContactState.Closed);
    }

    public Task<int?> GetRemainingAutoLockSecondsAsync(string doorId, CancellationToken ct = default)
    {
        if (_autoLockStateMachines.TryGetValue(doorId, out var sm))
        {
            return Task.FromResult(sm.RemainingSeconds);
        }

        return Task.FromResult<int?>(null);
    }

    public async Task<bool> UnlockDoorAsync(string doorId, int? durationMinutes = null, CancellationToken ct = default)
    {
        var door = await _doorRepo.GetByIdAsync(doorId, ct);
        if (door == null)
        {
            _logger.LogWarning("Cannot unlock door: door '{DoorId}' not found", doorId);
            return false;
        }

        var provider = CreateLockProvider(door);
        var success = await provider.UnlockAsync(ct);
        if (success)
        {
            _lockStates[doorId] = LockState.Unlocked;

            var sm = GetOrCreateStateMachine(door);
            if (durationMinutes.HasValue && durationMinutes.Value > 0)
            {
                sm.DaySeconds = durationMinutes.Value * 60;
                sm.NightSeconds = durationMinutes.Value * 60;
            }

            sm.OnLockStateChanged(LockState.Unlocked);
            _logger.LogInformation("Successfully unlocked door '{DoorName}' ({DoorId})", door.Name, doorId);
        }

        return success;
    }

    public async Task<bool> LockDoorAsync(string doorId, CancellationToken ct = default)
    {
        var door = await _doorRepo.GetByIdAsync(doorId, ct);
        if (door == null)
        {
            _logger.LogWarning("Cannot lock door: door '{DoorId}' not found", doorId);
            return false;
        }

        var provider = CreateLockProvider(door);
        var success = await provider.LockAsync(ct);
        if (success)
        {
            _lockStates[doorId] = LockState.Locked;

            if (_autoLockStateMachines.TryGetValue(doorId, out var sm))
            {
                sm.OnLockStateChanged(LockState.Locked);
            }

            _logger.LogInformation("Successfully locked door '{DoorName}' ({DoorId})", door.Name, doorId);
        }

        return success;
    }

    public void UpdateDoorStates(string doorId, LockState? lockState = null, DoorContactState? contactState = null)
    {
        if (!_autoLockStateMachines.ContainsKey(doorId))
        {
            try
            {
                var door = _doorRepo.GetByIdAsync(doorId).GetAwaiter().GetResult();
                if (door != null)
                {
                    GetOrCreateStateMachine(door);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to pre-initialize state machine for door '{DoorId}'", doorId);
            }
        }

        if (lockState.HasValue)
        {
            _lockStates[doorId] = lockState.Value;
            if (_autoLockStateMachines.TryGetValue(doorId, out var sm))
            {
                sm.OnLockStateChanged(lockState.Value);
            }
        }

        if (contactState.HasValue)
        {
            _contactStates[doorId] = contactState.Value;
            if (_autoLockStateMachines.TryGetValue(doorId, out var sm))
            {
                sm.OnDoorContactChanged(contactState.Value);
            }
        }
    }

    public async Task<int> ClearUserHardwareSlotsAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || _slotRepo == null)
        {
            return 0;
        }

        var doors = await _doorRepo.GetAllAsync(ct);
        var clearedCount = 0;

        foreach (var door in doors)
        {
            var slots = await _slotRepo.GetSlotsForDoorAsync(door.Id, ct);
            var userSlots = slots.Where(s => s.UserId == userId).ToList();
            if (userSlots.Count == 0)
            {
                continue;
            }

            var provider = CreateLockProvider(door);

            foreach (var slot in userSlots)
            {
                _logger.LogInformation("Clearing physical hardware slot {SlotNumber} for user {UserId} on door '{DoorName}' ({DoorId})",
                    slot.SlotNumber, userId, door.Name, door.Id);

                await _slotRepo.UpdateSlotSyncStatusAsync(slot.Id, SlotSyncStatus.Deleting, null, ct);

                var cleared = true;
                if (provider.Capabilities.HasFlag(LockCapabilities.UserCodes))
                {
                    cleared = await provider.ClearSlotCodeAsync(slot.SlotNumber, ct);
                }

                if (cleared)
                {
                    await _slotRepo.ClearSlotAsync(slot.Id, ct);
                    await _slotRepo.UpdateSlotSyncStatusAsync(slot.Id, SlotSyncStatus.Synced, DateTime.UtcNow, ct);
                    clearedCount++;
                }
                else
                {
                    _logger.LogWarning("Failed to clear physical hardware slot {SlotNumber} on door '{DoorName}'", slot.SlotNumber, door.Name);
                    await _slotRepo.UpdateSlotSyncStatusAsync(slot.Id, SlotSyncStatus.Error, null, ct);
                }
            }
        }

        return clearedCount;
    }

    private AutoLockStateMachine GetOrCreateStateMachine(AccessPoint door)
    {
        return _autoLockStateMachines.GetOrAdd(door.Id, id =>
        {
            var machine = new AutoLockStateMachine(
                door,
                onLockRequested: async () =>
                {
                    _logger.LogInformation("Auto-lock timer triggered for door '{DoorName}' ({DoorId})", door.Name, id);
                    return await LockDoorAsync(id);
                });
            return machine;
        });
    }

    private ILockProvider CreateLockProvider(AccessPoint door)
    {
        var configJson = door.LockConfigJson;
        string? topic = null;
        if (!string.IsNullOrWhiteSpace(configJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(configJson);
                if (doc.RootElement.TryGetProperty("topic", out var tProp))
                {
                    topic = tProp.GetString();
                }
                else if (doc.RootElement.TryGetProperty("lockTopic", out var ltProp))
                {
                    topic = ltProp.GetString();
                }
                else if (doc.RootElement.TryGetProperty("nodeId", out var nProp))
                {
                    topic = nProp.GetString();
                }
            }
            catch
            {
                // Fallback
            }
        }

        var lockType = door.LockProviderType ?? string.Empty;
        if (lockType.Contains("ZWave", StringComparison.OrdinalIgnoreCase))
        {
            return new ZWaveJsMqttLockProvider(topic ?? door.Id, 0, _mqttClient);
        }

        var cmdTopic = topic ?? $"codemaster/{door.Id}/lock/set";
        var stateTopic = topic ?? $"codemaster/{door.Id}/lock/state";
        return new GenericMqttLockProvider(cmdTopic, stateTopic, "LOCK", "UNLOCK", _mqttClient);
    }
}
