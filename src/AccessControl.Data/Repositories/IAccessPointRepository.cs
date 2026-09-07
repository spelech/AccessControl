using AccessControl.Core.Models;

namespace AccessControl.Data.Repositories;

public interface IAccessPointRepository
{
    Task<AccessPoint?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<AccessPoint>> GetAllAsync(CancellationToken ct = default);
    Task InsertAsync(AccessPoint accessPoint, CancellationToken ct = default);
    Task UpdateAsync(AccessPoint accessPoint, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
