using CodeMaster.Core.DTOs;
using CodeMaster.Core.Interfaces;
using CodeMaster.Core.Models;
using CodeMaster.Core.Transports;

namespace CodeMaster.Engine.Transports;

public class TransportLockProviderAdapter : ILockProvider
{
    private readonly ILockTransport _transport;
    private readonly string _deviceTarget;

    public TransportLockProviderAdapter(ILockTransport transport, string deviceTarget)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _deviceTarget = string.IsNullOrWhiteSpace(deviceTarget) ? "1" : deviceTarget;
    }

    public LockCapabilities Capabilities =>
        LockCapabilities.SupportsHardwareSlots |
        LockCapabilities.SupportsRemoteLock |
        LockCapabilities.SupportsRemoteUnlock |
        LockCapabilities.SupportsJammedReport;

    public Task<bool> LockAsync(CancellationToken ct = default) =>
        _transport.SetLockStateAsync(_deviceTarget, true, ct);

    public Task<bool> UnlockAsync(CancellationToken ct = default) =>
        _transport.SetLockStateAsync(_deviceTarget, false, ct);

    public Task<LockState> GetStateAsync(CancellationToken ct = default) =>
        _transport.GetLockStateAsync(_deviceTarget, ct);

    public Task<bool> SetSlotCodeAsync(int slotNumber, string pin, string? label = null, CancellationToken ct = default) =>
        _transport.SetUserCodeAsync(_deviceTarget, slotNumber, pin, label, ct);

    public Task<bool> ClearSlotCodeAsync(int slotNumber, CancellationToken ct = default) =>
        _transport.ClearUserCodeAsync(_deviceTarget, slotNumber, ct);

    public Task<IReadOnlyList<HardwareSlotDto>> GetSlotCodesAsync(CancellationToken ct = default) =>
        _transport.GetUserCodesAsync(_deviceTarget, ct);
}
