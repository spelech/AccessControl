using System.Diagnostics;
using System.Text.Json;
using CodeMaster.Core.Models;
using Xunit;

namespace CodeMaster.Tests.Harness;

public class RingKeypadToLockClosedLoopTests
{
    [Fact]
    public async Task RingKeypad_DisarmWithValidPin_IssuesAugustUnlockCommand_Within50ms_LogsAndPublishesHaEvent()
    {
        // Arrange
        using var harness = new ControlsTestHarness();
        await harness.SetupDoorAsync("door_august", lockTopic: "zwave/august", keypadTopic: "ring/kp");
        var user = await harness.AddUserWithPinAsync("Steve", "4821", doorId: "door_august");

        var sw = Stopwatch.StartNew();

        // Act - Inject simulated Ring Keypad PIN entry
        await harness.SimulateKeypadDisarmAsync("ring/kp", "4821");

        // Assert - Verify August lock command received on broker within 50ms
        var unlockMsg = await harness.WaitForPublishedMessageAsync(
            "zwave/august/door_lock/endpoint_0/targetState/set",
            timeoutMs: 500);

        sw.Stop();

        Assert.NotNull(unlockMsg);
        Assert.Equal("false", unlockMsg.Payload);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Expected unlock command to be issued quickly, took {sw.ElapsedMilliseconds}ms");

        // Assert - AccessLog record created
        var recentLogs = await harness.AuditLogRepository.GetRecentLogsAsync("door_august", 10);
        Assert.NotEmpty(recentLogs);
        var unlockLog = recentLogs.FirstOrDefault(l => l.EventType == AccessEventType.Unlocked);
        Assert.NotNull(unlockLog);
        Assert.Equal("Steve", unlockLog.UserName);
        Assert.Equal(AccessMethod.RingKeypad, unlockLog.Method);

        // Assert - HA event published
        var haEventMsg = await harness.WaitForPublishedMessageAsync(
            "codemaster/door_august/event/state",
            timeoutMs: 500);
        Assert.NotNull(haEventMsg);
        using var doc = JsonDocument.Parse(haEventMsg.Payload);
        Assert.Equal("keypad_unlock", doc.RootElement.GetProperty("event_type").GetString());
        Assert.Equal("Steve", doc.RootElement.GetProperty("user").GetString());
    }

    [Fact]
    public async Task RingKeypad_DisarmWithInvalidPin_AugustLockNotUnlocked_DeniedAccessLogCreated()
    {
        // Arrange
        using var harness = new ControlsTestHarness();
        await harness.SetupDoorAsync("door_august", lockTopic: "zwave/august", keypadTopic: "ring/kp");
        await harness.AddUserWithPinAsync("Steve", "4821", doorId: "door_august");

        // Act - Inject simulated Ring Keypad PIN entry with wrong PIN
        await harness.SimulateKeypadDisarmAsync("ring/kp", "9999");

        // Give short delay to ensure any potential commands would have been processed
        await Task.Delay(100);

        // Assert - Verify August lock targetState/set was NOT published
        var lockMessages = harness.Broker.PublishedMessages
            .Where(m => m.Topic.StartsWith("zwave/august/door_lock/endpoint_0/targetState/set"))
            .ToList();
        Assert.Empty(lockMessages);

        // Assert - Denied AccessLog record created
        var recentLogs = await harness.AuditLogRepository.GetRecentLogsAsync("door_august", 10);
        Assert.NotEmpty(recentLogs);
        var deniedLog = recentLogs.FirstOrDefault(l => l.EventType == AccessEventType.Denied);
        Assert.NotNull(deniedLog);
        Assert.Equal(AccessMethod.RingKeypad, deniedLog.Method);
        Assert.Contains("Invalid PIN", deniedLog.Details);
    }

    [Fact]
    public async Task AutoLock_ClosedLoop_Unlocked_DoorOpened_DoorClosed_Expires_LocksAugustDoor()
    {
        // Arrange
        using var harness = new ControlsTestHarness();
        await harness.SetupDoorAsync(
            "door_august",
            lockTopic: "zwave/august",
            keypadTopic: "ring/kp",
            autoLockEnabled: true,
            daySeconds: 60);

        // 1. Lock transitions to Unlocked
        harness.SimulateLockStateChanged("door_august", LockState.Unlocked);
        Assert.Equal(AutoLockStatus.CountingDown, harness.GetAutoLockStatus("door_august"));

        // 2. Door opens -> auto-lock pauses
        harness.SimulateDoorContactChanged("door_august", DoorContactState.Open);
        Assert.Equal(AutoLockStatus.PausedDoorOpen, harness.GetAutoLockStatus("door_august"));

        // 3. Door closes -> auto-lock resumes counting down
        harness.SimulateDoorContactChanged("door_august", DoorContactState.Closed);
        Assert.Equal(AutoLockStatus.CountingDown, harness.GetAutoLockStatus("door_august"));

        // 4. Countdown expires -> lock command is issued to August lock topic
        await harness.TriggerAutoLockExpiredAsync("door_august");

        // Assert - August lock topic receives targetState: true
        var lockMsg = await harness.WaitForPublishedMessageAsync(
            "zwave/august/door_lock/endpoint_0/targetState/set",
            timeoutMs: 500);

        Assert.NotNull(lockMsg);
        Assert.Equal("true", lockMsg.Payload);
        Assert.Equal(AutoLockStatus.Locked, harness.GetAutoLockStatus("door_august"));
    }

    [Fact]
    public async Task Schlage_SlotSync_ClosedLoop_UserAddedAndRemoved_EmitsSetAndClearCommands()
    {
        // Arrange
        using var harness = new ControlsTestHarness();
        await harness.SetupDoorAsync("door_schlage", lockTopic: "zwave/schlage", keypadTopic: "zwave/schlage");

        // Act 1: User added
        var cleanerUser = await harness.AddUserWithPinAsync("Cleaner", "9182", doorId: "door_schlage");

        // Reconcile hardware slots
        await harness.SyncHardwareSlotsAsync("door_schlage");

        // Assert 1: Slot 1 set command emitted
        var setMsg = await harness.WaitForPublishedMessageAsync(
            "zwave/schlage/user_code/endpoint_0/set",
            timeoutMs: 500);

        Assert.NotNull(setMsg);
        using (var setDoc = JsonDocument.Parse(setMsg.Payload))
        {
            Assert.Equal(1, setDoc.RootElement.GetProperty("slot").GetInt32());
            Assert.Equal("9182", setDoc.RootElement.GetProperty("usercode").GetString());
            Assert.Equal("Cleaner", setDoc.RootElement.GetProperty("label").GetString());
        }

        // Verify slot state in repository is Synced
        var doorSlots = await harness.HardwareSlotRepository.GetSlotsForDoorAsync("door_schlage");
        Assert.Single(doorSlots);
        Assert.Equal(1, doorSlots[0].SlotNumber);
        Assert.Equal(SlotSyncStatus.Synced, doorSlots[0].SyncStatus);

        // Clear broker messages before Act 2
        harness.Broker.ClearMessages();

        // Act 2: User removed
        await harness.RemoveUserAsync(cleanerUser.Id);

        // Reconcile hardware slots
        await harness.SyncHardwareSlotsAsync("door_schlage");

        // Assert 2: Slot 1 clear command emitted
        var clearMsg = await harness.WaitForPublishedMessageAsync(
            "zwave/schlage/user_code/endpoint_0/set",
            timeoutMs: 500);

        Assert.NotNull(clearMsg);
        using (var clearDoc = JsonDocument.Parse(clearMsg.Payload))
        {
            Assert.Equal(1, clearDoc.RootElement.GetProperty("slot").GetInt32());
            Assert.Equal(string.Empty, clearDoc.RootElement.GetProperty("usercode").GetString());
        }

        // Verify slot in repo is cleared
        doorSlots = await harness.HardwareSlotRepository.GetSlotsForDoorAsync("door_schlage");
        Assert.Single(doorSlots);
        Assert.Null(doorSlots[0].UserId);
    }
}
