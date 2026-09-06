using CodeMaster.Core.Models;

namespace CodeMaster.Data.Repositories;

public interface IAccessPolicyRepository
{
    Task<AccessPolicy?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<AccessPolicy>> GetByAccessPointIdAsync(string accessPointId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessPolicy>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task InsertAsync(AccessPolicy policy, CancellationToken ct = default);
    Task UpdateAsync(AccessPolicy policy, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task AssignPolicyAsync(AccessAssignment assignment, CancellationToken ct = default);
    Task RemoveAssignmentAsync(string assignmentId, CancellationToken ct = default);
}
