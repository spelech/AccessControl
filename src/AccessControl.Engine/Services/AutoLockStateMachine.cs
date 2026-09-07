using AccessControl.Core.Models;

namespace AccessControl.Engine.Services;

public class AutoLockStateMachine : IDisposable
{
    private readonly object _syncLock = new();
    private readonly TimeProvider _timeProvider;
    private readonly Func<Task<bool>>? _onLockRequested;
    private ITimer? _countdownTimer;
    private ITimer? _retryTimer;
    private bool _disposed;

    public AutoLockStateMachine(
        int timeoutSeconds = 60,
        int? nightSeconds = null,
        bool retryOnFailure = true,
        int retryDelaySeconds = 15,
        Func<Task<bool>>? onLockRequested = null,
        TimeProvider? timeProvider = null)
    {
        DaySeconds = timeoutSeconds;
        NightSeconds = nightSeconds ?? timeoutSeconds;
        RetryOnFailure = retryOnFailure;
        RetryDelaySeconds = retryDelaySeconds;
        _onLockRequested = onLockRequested;
        _timeProvider = timeProvider ?? TimeProvider.System;
        Status = AutoLockStatus.Locked;
        LockState = LockState.Locked;
        DoorContactState = DoorContactState.Closed;
    }

    public AutoLockStateMachine(
        AccessPoint accessPoint,
        Func<Task<bool>>? onLockRequested = null,
        TimeProvider? timeProvider = null)
        : this(
            timeoutSeconds: accessPoint.AutoLockDaySeconds,
            nightSeconds: accessPoint.AutoLockNightSeconds,
            retryOnFailure: accessPoint.RetryOnFailure,
            retryDelaySeconds: 15,
            onLockRequested: onLockRequested,
            timeProvider: timeProvider)
    {
        IsEnabled = accessPoint.AutoLockEnabled;
    }

    public AutoLockStatus Status { get; private set; }
    public LockState LockState { get; private set; }
    public DoorContactState DoorContactState { get; private set; }
    public bool IsEnabled { get; set; } = true;
    public int DaySeconds { get; set; }
    public int NightSeconds { get; set; }
    public bool RetryOnFailure { get; set; }
    public int RetryDelaySeconds { get; set; }
    public bool IsNight { get; set; }
    private DateTimeOffset? _countdownExpiresAt;

    public int? RemainingSeconds
    {
        get
        {
            lock (_syncLock)
            {
                if (Status != AutoLockStatus.CountingDown || !_countdownExpiresAt.HasValue)
                {
                    return null;
                }
                var diff = (_countdownExpiresAt.Value - _timeProvider.GetUtcNow()).TotalSeconds;
                return Math.Max(0, (int)Math.Ceiling(diff));
            }
        }
    }

    public event Action<AutoLockStatus>? StatusChanged;
    public event Func<Task<bool>>? LockRequested;

    public void OnLockStateChanged(LockState newState)
    {
        lock (_syncLock)
        {
            LockState = newState;

            if (!IsEnabled)
            {
                CancelAllTimers();
                SetStatus(AutoLockStatus.Disabled);
                return;
            }

            switch (newState)
            {
                case LockState.Locked:
                    CancelAllTimers();
                    SetStatus(AutoLockStatus.Locked);
                    break;

                case LockState.Unlocked:
                    CancelRetryTimer();
                    if (DoorContactState == DoorContactState.Open)
                    {
                        CancelCountdownTimer();
                        SetStatus(AutoLockStatus.PausedDoorOpen);
                    }
                    else
                    {
                        ArmCountdownTimer();
                        SetStatus(AutoLockStatus.CountingDown);
                    }
                    break;

                case LockState.Jammed:
                    CancelCountdownTimer();
                    if (RetryOnFailure)
                    {
                        SetStatus(AutoLockStatus.JammedRetry);
                        ArmRetryTimer();
                    }
                    else
                    {
                        CancelAllTimers();
                        SetStatus(AutoLockStatus.Idle);
                    }
                    break;

                default:
                    break;
            }
        }
    }

    public void OnDoorContactChanged(DoorContactState newState)
    {
        lock (_syncLock)
        {
            DoorContactState = newState;

            if (!IsEnabled)
            {
                CancelAllTimers();
                SetStatus(AutoLockStatus.Disabled);
                return;
            }

            if (LockState != LockState.Unlocked)
            {
                return;
            }

            if (newState == DoorContactState.Open)
            {
                CancelCountdownTimer();
                SetStatus(AutoLockStatus.PausedDoorOpen);
            }
            else if (newState == DoorContactState.Closed)
            {
                ArmCountdownTimer();
                SetStatus(AutoLockStatus.CountingDown);
            }
        }
    }

    public async Task TriggerCountdownExpiredAsync()
    {
        CancelCountdownTimer();
        await ExecuteLockAttemptAsync();
    }

    public async Task TriggerRetryExpiredAsync()
    {
        CancelRetryTimer();
        await ExecuteLockAttemptAsync();
    }

    private async Task ExecuteLockAttemptAsync()
    {
        bool success;
        try
        {
            if (_onLockRequested != null)
            {
                success = await _onLockRequested();
            }
            else if (LockRequested != null)
            {
                success = await LockRequested();
            }
            else
            {
                success = true;
            }
        }
        catch
        {
            success = false;
        }

        lock (_syncLock)
        {
            if (success)
            {
                CancelAllTimers();
                LockState = LockState.Locked;
                SetStatus(AutoLockStatus.Locked);
            }
            else
            {
                if (RetryOnFailure)
                {
                    SetStatus(AutoLockStatus.JammedRetry);
                    ArmRetryTimer();
                }
                else
                {
                    CancelAllTimers();
                    SetStatus(AutoLockStatus.Idle);
                }
            }
        }
    }

    private void ArmCountdownTimer()
    {
        CancelCountdownTimer();
        var delay = TimeSpan.FromSeconds(IsNight ? NightSeconds : DaySeconds);
        _countdownExpiresAt = _timeProvider.GetUtcNow().Add(delay);
        _countdownTimer = _timeProvider.CreateTimer(
            _ => _ = TriggerCountdownExpiredAsync(),
            null,
            delay,
            Timeout.InfiniteTimeSpan);
    }

    private void ArmRetryTimer()
    {
        CancelRetryTimer();
        var delay = TimeSpan.FromSeconds(RetryDelaySeconds);
        _retryTimer = _timeProvider.CreateTimer(
            _ => _ = TriggerRetryExpiredAsync(),
            null,
            delay,
            Timeout.InfiniteTimeSpan);
    }

    private void CancelCountdownTimer()
    {
        _countdownExpiresAt = null;
        _countdownTimer?.Dispose();
        _countdownTimer = null;
    }

    private void CancelRetryTimer()
    {
        _retryTimer?.Dispose();
        _retryTimer = null;
    }

    private void CancelAllTimers()
    {
        CancelCountdownTimer();
        CancelRetryTimer();
    }

    private void SetStatus(AutoLockStatus newStatus)
    {
        if (Status != newStatus)
        {
            Status = newStatus;
            StatusChanged?.Invoke(newStatus);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelAllTimers();
    }
}
