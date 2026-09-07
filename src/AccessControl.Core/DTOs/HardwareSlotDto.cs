namespace AccessControl.Core.DTOs;

public record HardwareSlotDto(
    int SlotNumber,
    bool InUse,
    string? PinCode = null,
    string? Label = null
);
