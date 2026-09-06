namespace CodeMaster.Core.Models;

public class AccessPoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string LockProviderType { get; set; } = string.Empty;
    public string LockConfigJson { get; set; } = "{}";
    public string? KeypadProviderType { get; set; }
    public string? KeypadConfigJson { get; set; }
    public string? DoorSensorProviderType { get; set; }
    public string? DoorSensorConfigJson { get; set; }
    public bool AutoLockEnabled { get; set; } = true;
    public int AutoLockDaySeconds { get; set; } = 300;
    public int AutoLockNightSeconds { get; set; } = 60;
    public bool RetryOnFailure { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
