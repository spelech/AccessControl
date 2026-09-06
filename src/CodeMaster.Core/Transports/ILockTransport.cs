using CodeMaster.Core.DTOs;
using CodeMaster.Core.Models;

namespace CodeMaster.Core.Transports;

public interface ILockTransport : ITransport
{
    Task<bool> SetLockStateAsync(string deviceTarget, bool locked, CancellationToken ct = default);
    Task<LockState> GetLockStateAsync(string deviceTarget, CancellationToken ct = default);
    Task<bool> SetUserCodeAsync(string deviceTarget, int slot, string pin, string? label, CancellationToken ct = default);
    Task<bool> ClearUserCodeAsync(string deviceTarget, int slot, CancellationToken ct = default);
    Task<IReadOnlyList<HardwareSlotDto>> GetUserCodesAsync(string deviceTarget, CancellationToken ct = default);

    event Action<LockStateUpdatedEventArgs>? OnLockStateChanged;
}
