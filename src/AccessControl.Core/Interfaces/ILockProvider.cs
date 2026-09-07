namespace AccessControl.Core.Interfaces;

using AccessControl.Core.DTOs;
using AccessControl.Core.Models;

[Flags]
public enum LockCapabilities
{
    None = 0,
    RemoteControl = 1 << 0,
    UserCodes = 1 << 1,
    AutoLock = 1 << 2,
    JamDetection = 1 << 3,

    SupportsHardwareSlots = UserCodes,
    SupportsRemoteLock = RemoteControl,
    SupportsRemoteUnlock = RemoteControl,
    SupportsJammedReport = JamDetection
}

public interface ILockProvider
{
    LockCapabilities Capabilities { get; }
    Task<LockState> GetStateAsync(CancellationToken cancellationToken = default);
    Task<bool> LockAsync(CancellationToken cancellationToken = default);
    Task<bool> UnlockAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HardwareSlotDto>> GetSlotCodesAsync(CancellationToken cancellationToken = default);
    Task<bool> SetSlotCodeAsync(int slotNumber, string pinCode, string? label = null, CancellationToken cancellationToken = default);
    Task<bool> ClearSlotCodeAsync(int slotNumber, CancellationToken cancellationToken = default);
}
