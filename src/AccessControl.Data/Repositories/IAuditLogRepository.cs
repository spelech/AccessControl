using AccessControl.Core.Models;

namespace AccessControl.Data.Repositories;

public interface IAuditLogRepository
{
    Task InsertAsync(AccessLog log, CancellationToken ct = default);
    Task<IReadOnlyList<AccessLog>> GetRecentLogsAsync(string accessPointId, int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<AccessLog>> GetAllRecentLogsAsync(int limit = 100, CancellationToken ct = default);
}
