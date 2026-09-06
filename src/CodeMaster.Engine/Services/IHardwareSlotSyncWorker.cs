using CodeMaster.Core.Interfaces;
using CodeMaster.Core.Models;

namespace CodeMaster.Engine.Services;

public interface IHardwareSlotSyncWorker
{
    Task ReconcileDoorSlotsAsync(
        AccessPoint accessPoint,
        ILockProvider lockProvider,
        DateTime? checkTimeUtc = null,
        CancellationToken cancellationToken = default);
}
