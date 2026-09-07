using AccessControl.Core.Models;
using AccessControl.Engine.Services;
using Xunit;

namespace AccessControl.Tests.Unit;

public class AutoLockStateMachineTests
{
    [Fact]
    public void AutoLock_SuspendsWhenDoorIsOpen_ResumesWhenClosed()
    {
        var sm = new AutoLockStateMachine(timeoutSeconds: 60);

        sm.OnLockStateChanged(LockState.Unlocked);
        sm.OnDoorContactChanged(DoorContactState.Open);
        Assert.Equal(AutoLockStatus.PausedDoorOpen, sm.Status);

        sm.OnDoorContactChanged(DoorContactState.Closed);
        Assert.Equal(AutoLockStatus.CountingDown, sm.Status);
    }

    [Fact]
    public void AutoLock_DoorOpenedDuringCountdown_CancelsTimerAndPauses()
    {
        var sm = new AutoLockStateMachine(timeoutSeconds: 60);

        // Lock unlocks with door closed -> begins countdown
        sm.OnDoorContactChanged(DoorContactState.Closed);
        sm.OnLockStateChanged(LockState.Unlocked);
        Assert.Equal(AutoLockStatus.CountingDown, sm.Status);

        // Door opens while counting down -> timer cancels and status becomes PausedDoorOpen
        sm.OnDoorContactChanged(DoorContactState.Open);
        Assert.Equal(AutoLockStatus.PausedDoorOpen, sm.Status);
    }

    [Fact]
    public async Task AutoLock_WhenCountdownExpires_FiresLockRequest()
    {
        var lockRequested = false;
        var sm = new AutoLockStateMachine(
            timeoutSeconds: 60,
            onLockRequested: () =>
            {
                lockRequested = true;
                return Task.FromResult(true);
            });

        sm.OnDoorContactChanged(DoorContactState.Closed);
        sm.OnLockStateChanged(LockState.Unlocked);
        Assert.Equal(AutoLockStatus.CountingDown, sm.Status);

        await sm.TriggerCountdownExpiredAsync();

        Assert.True(lockRequested);
        Assert.Equal(AutoLockStatus.Locked, sm.Status);
    }

    [Fact]
    public async Task AutoLock_LockFailure_TriggersJamRetry_WhenRetryEnabled()
    {
        var attempts = 0;
        var sm = new AutoLockStateMachine(
            timeoutSeconds: 60,
            retryOnFailure: true,
            retryDelaySeconds: 15,
            onLockRequested: () =>
            {
                attempts++;
                return Task.FromResult(false); // Lock attempt failed / jammed
            });

        sm.OnDoorContactChanged(DoorContactState.Closed);
        sm.OnLockStateChanged(LockState.Unlocked);

        await sm.TriggerCountdownExpiredAsync();

        Assert.Equal(1, attempts);
        Assert.Equal(AutoLockStatus.JammedRetry, sm.Status);
    }

    [Fact]
    public void AutoLock_OnLockStateJammed_EntersJammedRetry()
    {
        var sm = new AutoLockStateMachine(timeoutSeconds: 60, retryOnFailure: true);

        sm.OnLockStateChanged(LockState.Jammed);

        Assert.Equal(AutoLockStatus.JammedRetry, sm.Status);
    }

    [Fact]
    public void AutoLock_WhenDisabled_StatusIsDisabled()
    {
        var sm = new AutoLockStateMachine(timeoutSeconds: 60)
        {
            IsEnabled = false
        };

        sm.OnLockStateChanged(LockState.Unlocked);
        Assert.Equal(AutoLockStatus.Disabled, sm.Status);
    }
}
