using CodeMaster.Core.Models;

namespace CodeMaster.Core.Interfaces;

public interface IDoorOperationService
{
    Task<LockState> GetDoorLockStateAsync(string doorId, CancellationToken ct = default);
    Task<DoorContactState> GetDoorContactStateAsync(string doorId, CancellationToken ct = default);
    Task<int?> GetRemainingAutoLockSecondsAsync(string doorId, CancellationToken ct = default);
    Task<bool> UnlockDoorAsync(string doorId, int? durationMinutes = null, CancellationToken ct = default);
    Task<bool> LockDoorAsync(string doorId, CancellationToken ct = default);
    void UpdateDoorStates(string doorId, LockState? lockState = null, DoorContactState? contactState = null);
}
