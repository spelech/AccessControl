using CodeMaster.Core.Interfaces;
using CodeMaster.Core.Models;
using CodeMaster.Data.Repositories;
using CodeMaster.Engine.Services;
using NSubstitute;
using Xunit;

namespace CodeMaster.Tests.Unit;

public class HardwareSlotSyncWorkerTests
{
    private readonly IHardwareSlotRepository _slotRepo;
    private readonly IUserRepository _userRepo;
    private readonly ICredentialRepository _credentialRepo;
    private readonly IAccessPolicyRepository _policyRepo;
    private readonly ILockProvider _lockProvider;
    private readonly HardwareSlotSyncWorker _worker;

    public HardwareSlotSyncWorkerTests()
    {
        _slotRepo = Substitute.For<IHardwareSlotRepository>();
        _userRepo = Substitute.For<IUserRepository>();
        _credentialRepo = Substitute.For<ICredentialRepository>();
        _policyRepo = Substitute.For<IAccessPolicyRepository>();
        _lockProvider = Substitute.For<ILockProvider>();
        _worker = new HardwareSlotSyncWorker(_slotRepo, _userRepo, _credentialRepo, _policyRepo);
    }

    [Fact]
    public async Task ReconcileDoorSlotsAsync_Skips_WhenLockDoesNotSupportUserCodes()
    {
        var door = new AccessPoint { Id = "door-generic", Name = "Generic Lock" };
        _lockProvider.Capabilities.Returns(LockCapabilities.RemoteControl); // No UserCodes flag

        await _worker.ReconcileDoorSlotsAsync(door, _lockProvider);

        await _lockProvider.DidNotReceive().SetSlotCodeAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _slotRepo.DidNotReceive().GetSlotsForDoorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileDoorSlotsAsync_AddsNewUserToFreeSlot()
    {
        var door = new AccessPoint { Id = "door-schlage", Name = "Front Door" };
        _lockProvider.Capabilities.Returns(LockCapabilities.SupportsHardwareSlots | LockCapabilities.RemoteControl);

        var user = new User { Id = "user-1", Name = "Steve", IsActive = true };
        var cred = new Credential { Id = "cred-1", UserId = user.Id, Type = CredentialType.PIN, EncryptedValue = "4821" };
        var policy = new AccessPolicy { Id = "policy-1", Name = "24/7", ScheduleType = ScheduleType.Always, IsEnabled = true };

        _userRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([user]);
        _credentialRepo.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns([cred]);
        _policyRepo.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns([policy]);
        _policyRepo.GetByAccessPointIdAsync(door.Id, Arg.Any<CancellationToken>()).Returns([policy]);
        _slotRepo.GetSlotsForDoorAsync(door.Id, Arg.Any<CancellationToken>()).Returns(new List<HardwareSlot>());

        var allocatedSlot = new HardwareSlot { Id = "slot-1", AccessPointId = door.Id, SlotNumber = 1, UserId = user.Id, CredentialId = cred.Id };
        _slotRepo.AllocateSlotAsync(door.Id, user.Id, cred.Id, 1, Arg.Any<CancellationToken>()).Returns(allocatedSlot);
        _lockProvider.SetSlotCodeAsync(1, "4821", "Steve", Arg.Any<CancellationToken>()).Returns(true);

        await _worker.ReconcileDoorSlotsAsync(door, _lockProvider);

        await _slotRepo.Received(1).AllocateSlotAsync(door.Id, user.Id, cred.Id, 1, Arg.Any<CancellationToken>());
        await _lockProvider.Received(1).SetSlotCodeAsync(1, "4821", "Steve", Arg.Any<CancellationToken>());
        await _slotRepo.Received(1).UpdateSlotSyncStatusAsync(allocatedSlot.Id, SlotSyncStatus.Synced, Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileDoorSlotsAsync_ClearsObsoleteUserSlot()
    {
        var door = new AccessPoint { Id = "door-schlage", Name = "Front Door" };
        _lockProvider.Capabilities.Returns(LockCapabilities.SupportsHardwareSlots);

        var existingSlot = new HardwareSlot
        {
            Id = "slot-2",
            AccessPointId = door.Id,
            SlotNumber = 2,
            UserId = "former-guest",
            CredentialId = "cred-guest",
            SyncStatus = SlotSyncStatus.Synced
        };

        // No active users or desired assignments returned
        _userRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<User>());
        _slotRepo.GetSlotsForDoorAsync(door.Id, Arg.Any<CancellationToken>()).Returns(new List<HardwareSlot> { existingSlot });
        _lockProvider.ClearSlotCodeAsync(2, Arg.Any<CancellationToken>()).Returns(true);

        await _worker.ReconcileDoorSlotsAsync(door, _lockProvider);

        await _lockProvider.Received(1).ClearSlotCodeAsync(2, Arg.Any<CancellationToken>());
        await _slotRepo.Received(1).ClearSlotAsync(existingSlot.Id, Arg.Any<CancellationToken>());
        await _slotRepo.Received(1).UpdateSlotSyncStatusAsync(existingSlot.Id, SlotSyncStatus.Synced, Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }
}
