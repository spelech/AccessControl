namespace CodeMaster.Core.DTOs;

public enum KeypadAction
{
    Disarm,
    ArmAway,
    ArmStay,
    Lock,
    Unlock,
    Custom
}

public record KeypadEventDto
{
    public KeypadAction Action { get; init; } = KeypadAction.Disarm;
    public string? Pin { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? RawTopic { get; init; }
    public string? DeviceId { get; init; }
}
