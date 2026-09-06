namespace CodeMaster.Core.Models;

public enum AutoLockStatus
{
    Disabled,
    Idle,
    CountingDown,
    PausedDoorOpen,
    JammedRetry,
    Locked
}
