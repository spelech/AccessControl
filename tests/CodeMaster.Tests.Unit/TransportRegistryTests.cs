using CodeMaster.Core.Transports;
using CodeMaster.Engine.Transports;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodeMaster.Tests.Unit;

public class TransportRegistryTests
{
    private class FakeLockTransport : ILockTransport
    {
        public string TransportId => "fake_lock";
        public string DisplayName => "Fake Lock Transport";
        public bool IsConnected { get; private set; }
        public TransportStatus Status { get; private set; } = TransportStatus.Disconnected;

        public event Action<TransportStatusChangedEventArgs>? OnStatusChanged;
        public event Action<LockStateUpdatedEventArgs>? OnLockStateChanged = delegate { };

        public Task StartAsync(CancellationToken ct = default)
        {
            IsConnected = true;
            Status = TransportStatus.Connected;
            OnStatusChanged?.Invoke(new TransportStatusChangedEventArgs(TransportId, TransportStatus.Disconnected, TransportStatus.Connected));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct = default)
        {
            IsConnected = false;
            Status = TransportStatus.Disconnected;
            return Task.CompletedTask;
        }

        public Task<bool> SetLockStateAsync(string deviceTarget, bool locked, CancellationToken ct = default) => Task.FromResult(true);
        public Task<CodeMaster.Core.Models.LockState> GetLockStateAsync(string deviceTarget, CancellationToken ct = default) => Task.FromResult(CodeMaster.Core.Models.LockState.Locked);
        public Task<bool> SetUserCodeAsync(string deviceTarget, int slot, string pin, string? label, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> ClearUserCodeAsync(string deviceTarget, int slot, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<CodeMaster.Core.DTOs.HardwareSlotDto>> GetUserCodesAsync(string deviceTarget, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CodeMaster.Core.DTOs.HardwareSlotDto>>(Array.Empty<CodeMaster.Core.DTOs.HardwareSlotDto>());
    }

    [Fact]
    public async Task RegisterAndResolve_ReturnsTypedTransport()
    {
        var registry = new TransportRegistry(NullLogger<TransportRegistry>.Instance);
        var fake = new FakeLockTransport();

        registry.RegisterTransport(fake);

        var retrieved = registry.GetTransport<ILockTransport>("fake_lock");
        Assert.NotNull(retrieved);
        Assert.Equal("fake_lock", retrieved.TransportId);

        var nonExistent = registry.GetTransport<ILockTransport>("missing");
        Assert.Null(nonExistent);

        await registry.StartAllAsync();
        Assert.True(fake.IsConnected);

        await registry.StopAllAsync();
        Assert.False(fake.IsConnected);
    }
}
