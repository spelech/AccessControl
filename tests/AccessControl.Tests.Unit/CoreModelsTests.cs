using AccessControl.Core.DTOs;
using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;
using Xunit;

namespace AccessControl.Tests.Unit;

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
    public void AccessPolicy_IsActiveAt_AlwaysSchedule_ReturnsTrueRegardlessOfTime()
    {
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.Always,
            IsEnabled = true
        };

        Assert.True(policy.IsActiveAt(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.True(policy.IsActiveAt(new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc)));
    }

    [Fact]
    public void AccessPolicy_IsActiveAt_InvalidTimeZone_GracefullyFallsBackToUtc()
    {
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.WeeklyRecurring,
            DaysOfWeek = 127,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            TimeZoneId = "Invalid/NonExistent_TimeZone",
            IsEnabled = true
        };

        // 12:00 UTC should be active in fallback UTC
        var testTime = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(policy.IsActiveAt(testTime));

        // 20:00 UTC should be inactive
        var afterTime = new DateTime(2026, 9, 7, 20, 0, 0, DateTimeKind.Utc);
        Assert.False(policy.IsActiveAt(afterTime));
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
}
