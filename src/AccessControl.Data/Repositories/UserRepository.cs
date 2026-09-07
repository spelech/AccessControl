using AccessControl.Core.Models;
using AccessControl.Data.Db;
using Dapper;

namespace AccessControl.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, GroupId, Name, Role, IsActive, CreatedAt, UpdatedAt
            FROM Users
            WHERE Id = @Id;";

        return await connection.QuerySingleOrDefaultAsync<User>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, GroupId, Name, Role, IsActive, CreatedAt, UpdatedAt
            FROM Users
            ORDER BY Name ASC;";

        var results = await connection.QueryAsync<User>(new CommandDefinition(sql, cancellationToken: ct));
        return results.ToList();
    }

    public async Task InsertAsync(User user, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO Users (Id, GroupId, Name, Role, IsActive, CreatedAt, UpdatedAt)
            VALUES (@Id, @GroupId, @Name, @Role, @IsActive, @CreatedAt, @UpdatedAt);";

        var parameters = new
        {
            user.Id,
            user.GroupId,
            user.Name,
            Role = user.Role.ToString(),
            IsActive = user.IsActive ? 1 : 0,
            CreatedAt = user.CreatedAt.ToString("O"),
            UpdatedAt = user.UpdatedAt.ToString("O")
        };

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Users
            SET GroupId = @GroupId,
                Name = @Name,
                Role = @Role,
                IsActive = @IsActive,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id;";

        var parameters = new
        {
            user.Id,
            user.GroupId,
            user.Name,
            Role = user.Role.ToString(),
            IsActive = user.IsActive ? 1 : 0,
            UpdatedAt = user.UpdatedAt.ToString("O")
        };

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM Users WHERE Id = @Id;";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }
}
