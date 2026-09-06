namespace CodeMaster.Core.DTOs;

public record HardwareSlotDto(
    int SlotNumber,
    bool InUse,
    string? PinCode = null,
    string? Label = null
);
