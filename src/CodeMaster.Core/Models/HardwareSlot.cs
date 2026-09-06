namespace CodeMaster.Core.Models;

public enum SlotSyncStatus
{
    Synced,
    Adding,
    Deleting,
    Error
}

public class HardwareSlot
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AccessPointId { get; set; } = string.Empty;
    public int SlotNumber { get; set; }
    public string? UserId { get; set; }
    public string? CredentialId { get; set; }
    public SlotSyncStatus SyncStatus { get; set; } = SlotSyncStatus.Synced;
    public DateTime? LastSyncedAt { get; set; }
}
