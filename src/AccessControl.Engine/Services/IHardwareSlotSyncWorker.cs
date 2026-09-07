using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;

namespace AccessControl.Engine.Services;

public interface IHardwareSlotSyncWorker
{
    Task ReconcileDoorSlotsAsync(
        AccessPoint accessPoint,
        ILockProvider lockProvider,
        DateTime? checkTimeUtc = null,
        CancellationToken cancellationToken = default);
}
