using CodeMaster.Core.Models;
using CodeMaster.Data.Db;
using Dapper;

namespace CodeMaster.Data.Repositories;

public class CredentialRepository : ICredentialRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CredentialRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Credential?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, UserId, Type, EncryptedValue, HashedValue, PinLength, Label, CreatedAt
            FROM Credentials
            WHERE Id = @Id;";

        return await connection.QuerySingleOrDefaultAsync<Credential>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Credential>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, UserId, Type, EncryptedValue, HashedValue, PinLength, Label, CreatedAt
            FROM Credentials
            WHERE UserId = @UserId
            ORDER BY CreatedAt ASC;";

        var results = await connection.QueryAsync<Credential>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));
        return results.ToList();
    }

    public async Task InsertAsync(Credential credential, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO Credentials (Id, UserId, Type, EncryptedValue, HashedValue, PinLength, Label, CreatedAt)
            VALUES (@Id, @UserId, @Type, @EncryptedValue, @HashedValue, @PinLength, @Label, @CreatedAt);";

        var parameters = new
        {
            credential.Id,
            credential.UserId,
            Type = credential.Type.ToString(),
            EncryptedValue = credential.EncryptedValue ?? string.Empty,
            credential.HashedValue,
            credential.PinLength,
            credential.Label,
            CreatedAt = credential.CreatedAt.ToString("O")
        };

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM Credentials WHERE Id = @Id;";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }
}
