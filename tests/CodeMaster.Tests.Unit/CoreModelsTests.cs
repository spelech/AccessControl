using CodeMaster.Core.DTOs;
using CodeMaster.Core.Interfaces;
using CodeMaster.Core.Models;
using Xunit;

namespace CodeMaster.Tests.Unit;

public class CoreModelsTests
{
    [Fact]
    public void AccessPolicy_IsActiveAt_ReturnsTrue_WithinWindow()
    {
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.WeeklyRecurring,
            DaysOfWeek = 127, // All days (Mon..Sun)
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            IsEnabled = true
        };

        var testTime = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc); // Monday 12:00
        Assert.True(policy.IsActiveAt(testTime));
    }

    [Fact]
    public void AccessPolicy_IsActiveAt_ReturnsFalse_OutsideWindow()
    {
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.WeeklyRecurring,
            DaysOfWeek = 127,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            IsEnabled = true
        };

        var testTime = new DateTime(2026, 9, 7, 18, 30, 0, DateTimeKind.Utc); // Monday 18:30
        Assert.False(policy.IsActiveAt(testTime));
    }

    [Fact]
    public void AccessPolicy_IsActiveAt_OvernightWindow_EvaluatesCorrectly()
    {
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.WeeklyRecurring,
            DaysOfWeek = 127,
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(6, 0),
            IsEnabled = true
        };

        // 23:30 is active
        var lateNight = new DateTime(2026, 9, 7, 23, 30, 0, DateTimeKind.Utc);
        Assert.True(policy.IsActiveAt(lateNight));

        // 04:30 is active
        var earlyMorning = new DateTime(2026, 9, 7, 4, 30, 0, DateTimeKind.Utc);
        Assert.True(policy.IsActiveAt(earlyMorning));

        // 14:00 is not active
        var afternoon = new DateTime(2026, 9, 7, 14, 0, 0, DateTimeKind.Utc);
        Assert.False(policy.IsActiveAt(afternoon));
    }

    [Fact]
    public void AccessPolicy_IsActiveAt_ReturnsFalse_DayMismatch()
    {
        // Day bitmask: 1=Mon, 2=Tue, 4=Wed, 8=Thu, 16=Fri, 32=Sat, 64=Sun
        // Only Mon (1) and Tue (2) enabled = 3
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.WeeklyRecurring,
            DaysOfWeek = 3, // Mon + Tue only
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            IsEnabled = true
        };

        // 2026-09-09 is Wednesday (4)
        var testTime = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(policy.IsActiveAt(testTime));
    }

    [Fact]
    public void AccessPolicy_IsActiveAt_SundayBitmask_EvaluatesCorrectly()
    {
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.WeeklyRecurring,
            DaysOfWeek = 64, // Sunday only
            IsEnabled = true
        };

        // 2026-09-06 is Sunday
        var sunday = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(policy.IsActiveAt(sunday));

        // 2026-09-07 is Monday
        var monday = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(policy.IsActiveAt(monday));
    }

    [Fact]
    public void AccessPolicy_IsActiveAt_DateRange_EvaluatesCorrectly()
    {
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.DateRange,
            ValidFrom = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ValidUntil = new DateTime(2026, 9, 5, 23, 59, 59, DateTimeKind.Utc),
            IsEnabled = true
        };

        var beforeTime = new DateTime(2026, 8, 31, 23, 59, 0, DateTimeKind.Utc);
        Assert.False(policy.IsActiveAt(beforeTime));

        var withinTime = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(policy.IsActiveAt(withinTime));

        var afterTime = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        Assert.False(policy.IsActiveAt(afterTime));
    }

    [Fact]
    public void AccessPolicy_IsActiveAt_ReturnsFalse_OneTimeWithZeroUses()
    {
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.OneTime,
            RemainingUses = 0,
            IsEnabled = true
        };

        var testTime = DateTime.UtcNow;
        Assert.False(policy.IsActiveAt(testTime));
    }

    [Fact]
    public void AccessPolicy_IsActiveAt_ReturnsTrue_OneTimeWithRemainingUses()
    {
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.OneTime,
            RemainingUses = 1,
            IsEnabled = true
        };

        var testTime = DateTime.UtcNow;
        Assert.True(policy.IsActiveAt(testTime));
    }

    [Fact]
    public void AccessPolicy_IsActiveAt_ReturnsFalse_WhenDisabled()
    {
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.Always,
            IsEnabled = false
        };

        Assert.False(policy.IsActiveAt(DateTime.UtcNow));
    }

    [Fact]
    public void AccessPoint_Defaults_AreProperlyConfigured()
    {
        var point = new AccessPoint();
        Assert.False(string.IsNullOrWhiteSpace(point.Id));
        Assert.True(point.AutoLockEnabled);
        Assert.Equal(300, point.AutoLockDaySeconds);
        Assert.Equal(60, point.AutoLockNightSeconds);
        Assert.True(point.RetryOnFailure);
        Assert.Equal("{}", point.LockConfigJson);
    }

    [Fact]
    public void Credential_Properties_AreInitializedCorrectly()
    {
        var cred = new Credential
        {
            Id = "cred_1",
            UserId = "user_1",
            Type = CredentialType.PIN,
            HashedValue = "argon2_or_sha256_hash",
            PinLength = 4,
            Label = "Front Door PIN"
        };

        Assert.Equal("cred_1", cred.Id);
        Assert.Equal("user_1", cred.UserId);
        Assert.Equal(CredentialType.PIN, cred.Type);
        Assert.Equal(4, cred.PinLength);
        Assert.Equal("Front Door PIN", cred.Label);
    }

    [Fact]
    public void User_Initialization_HasCorrectDefaults()
    {
        var user = new User
        {
            Name = "Alice Doe",
            Role = UserRole.Admin
        };

        Assert.False(string.IsNullOrWhiteSpace(user.Id));
        Assert.Equal("Alice Doe", user.Name);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void HardwareSlot_Initialization_HasCorrectDefaults()
    {
        var slot = new HardwareSlot
        {
            AccessPointId = "ap_1",
            SlotNumber = 3,
            SyncStatus = SlotSyncStatus.Synced
        };

        Assert.False(string.IsNullOrWhiteSpace(slot.Id));
        Assert.Equal("ap_1", slot.AccessPointId);
        Assert.Equal(3, slot.SlotNumber);
        Assert.Equal(SlotSyncStatus.Synced, slot.SyncStatus);
    }

    [Fact]
    public void AccessLog_Initialization_HasCorrectDefaults()
    {
        var log = new AccessLog
        {
            AccessPointId = "ap_front",
            UserId = "usr_1",
            UserName = "Alice",
            CredentialType = CredentialType.PIN,
            EventType = AccessEventType.Unlocked,
            Method = AccessMethod.RingKeypad
        };

        Assert.False(string.IsNullOrWhiteSpace(log.Id));
        Assert.Equal("ap_front", log.AccessPointId);
        Assert.Equal("Alice", log.UserName);
        Assert.Equal(AccessEventType.Unlocked, log.EventType);
        Assert.Equal(AccessMethod.RingKeypad, log.Method);
    }

    [Fact]
    public void ProviderCapabilities_Flags_SupportBitwiseOperations()
    {
        var lockCaps = LockCapabilities.RemoteControl | LockCapabilities.UserCodes | LockCapabilities.JamDetection;
        Assert.True(lockCaps.HasFlag(LockCapabilities.RemoteControl));
        Assert.True(lockCaps.HasFlag(LockCapabilities.UserCodes));
        Assert.False(lockCaps.HasFlag(LockCapabilities.AutoLock));
        Assert.True(lockCaps.HasFlag(LockCapabilities.JamDetection));

        var keypadCaps = KeypadCapabilities.ArmDisarm | KeypadCapabilities.StatelessPinEvents;
        Assert.True(keypadCaps.HasFlag(KeypadCapabilities.ArmDisarm));
        Assert.True(keypadCaps.HasFlag(KeypadCapabilities.StatelessPinEvents));
        Assert.False(keypadCaps.HasFlag(KeypadCapabilities.SlottedPinStorage));
    }

    [Fact]
    public void DTOs_HoldExpectedData()
    {
        var slotDto = new HardwareSlotDto(1, true, "1234", "Master Code");
        Assert.Equal(1, slotDto.SlotNumber);
        Assert.True(slotDto.InUse);
        Assert.Equal("1234", slotDto.PinCode);
        Assert.Equal("Master Code", slotDto.Label);

        var now = DateTime.UtcNow;
        var keypadEvent = new KeypadEventDto
        {
            Action = KeypadAction.Disarm,
            Pin = "9876",
            Timestamp = now,
            RawTopic = "ring/alarm/keypad",
            DeviceId = "keypad_v2"
        };

        Assert.Equal(KeypadAction.Disarm, keypadEvent.Action);
        Assert.Equal("9876", keypadEvent.Pin);
        Assert.Equal(now, keypadEvent.Timestamp);
        Assert.Equal("ring/alarm/keypad", keypadEvent.RawTopic);
        Assert.Equal("keypad_v2", keypadEvent.DeviceId);
    }
}
