namespace AccessControl.Core.Models;

public enum AccessEventType
{
    Unlocked,
    Locked,
    Denied,
    Jammed,
    AutoLocked
}

public enum AccessMethod
{
    RingKeypad,
    BuiltInKeypad,
    ZWaveKeypad,
    Manual,
    RF,
    AutoLock
}

public class AccessLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AccessPointId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public CredentialType? CredentialType { get; set; }
    public AccessEventType EventType { get; set; } = AccessEventType.Unlocked;
    public AccessMethod Method { get; set; } = AccessMethod.Manual;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
}
