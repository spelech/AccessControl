using AccessControl.Core.Models;

namespace AccessControl.Data.Repositories;

public interface ICredentialRepository
{
    Task<Credential?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Credential>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task InsertAsync(Credential credential, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
