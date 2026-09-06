using CodeMaster.Core.Models;

namespace CodeMaster.Data.Repositories;

public interface IHardwareSlotRepository
{
    Task<HardwareSlot?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<HardwareSlot?> GetSlotAsync(string accessPointId, int slotNumber, CancellationToken ct = default);
    Task<IReadOnlyList<HardwareSlot>> GetSlotsForDoorAsync(string accessPointId, CancellationToken ct = default);
    Task UpdateSlotSyncStatusAsync(string slotId, SlotSyncStatus status, DateTime? lastSyncedAt = null, CancellationToken ct = default);
    Task<HardwareSlot?> AllocateSlotAsync(string accessPointId, string userId, string credentialId, int slotNumber, CancellationToken ct = default);
    Task ClearSlotAsync(string slotId, CancellationToken ct = default);
    Task DeleteSlotAsync(string slotId, CancellationToken ct = default);
}
