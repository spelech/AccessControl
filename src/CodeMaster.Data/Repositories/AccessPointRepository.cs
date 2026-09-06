using CodeMaster.Core.Models;
using CodeMaster.Data.Db;
using Dapper;

namespace CodeMaster.Data.Repositories;

public class AccessPointRepository : IAccessPointRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AccessPointRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<AccessPoint?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, Name, LockProviderType, LockConfigJson,
                   KeypadProviderType, KeypadConfigJson,
                   DoorSensorProviderType, DoorSensorConfigJson,
                   AutoLockEnabled, AutoLockDaySeconds, AutoLockNightSeconds,
                   RetryOnFailure, CreatedAt, UpdatedAt
            FROM AccessPoints
            WHERE Id = @Id;";

        return await connection.QuerySingleOrDefaultAsync<AccessPoint>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AccessPoint>> GetAllAsync(CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, Name, LockProviderType, LockConfigJson,
                   KeypadProviderType, KeypadConfigJson,
                   DoorSensorProviderType, DoorSensorConfigJson,
                   AutoLockEnabled, AutoLockDaySeconds, AutoLockNightSeconds,
                   RetryOnFailure, CreatedAt, UpdatedAt
            FROM AccessPoints
            ORDER BY Name ASC;";

        var results = await connection.QueryAsync<AccessPoint>(new CommandDefinition(sql, cancellationToken: ct));
        return results.ToList();
    }

    public async Task InsertAsync(AccessPoint accessPoint, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO AccessPoints (
                Id, Name, LockProviderType, LockConfigJson,
                KeypadProviderType, KeypadConfigJson,
                DoorSensorProviderType, DoorSensorConfigJson,
                AutoLockEnabled, AutoLockDaySeconds, AutoLockNightSeconds,
                RetryOnFailure, CreatedAt, UpdatedAt
            )
            VALUES (
                @Id, @Name, @LockProviderType, @LockConfigJson,
                @KeypadProviderType, @KeypadConfigJson,
                @DoorSensorProviderType, @DoorSensorConfigJson,
                @AutoLockEnabled, @AutoLockDaySeconds, @AutoLockNightSeconds,
                @RetryOnFailure, @CreatedAt, @UpdatedAt
            );";

        var parameters = new
        {
            accessPoint.Id,
            accessPoint.Name,
            accessPoint.LockProviderType,
            LockConfigJson = string.IsNullOrWhiteSpace(accessPoint.LockConfigJson) ? "{}" : accessPoint.LockConfigJson,
            KeypadProviderType = accessPoint.KeypadProviderType ?? "None",
            accessPoint.KeypadConfigJson,
            DoorSensorProviderType = accessPoint.DoorSensorProviderType ?? "None",
            accessPoint.DoorSensorConfigJson,
            AutoLockEnabled = accessPoint.AutoLockEnabled ? 1 : 0,
            accessPoint.AutoLockDaySeconds,
            accessPoint.AutoLockNightSeconds,
            RetryOnFailure = accessPoint.RetryOnFailure ? 1 : 0,
            CreatedAt = accessPoint.CreatedAt.ToString("O"),
            UpdatedAt = accessPoint.UpdatedAt.ToString("O")
        };

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task UpdateAsync(AccessPoint accessPoint, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE AccessPoints
            SET Name = @Name,
                LockProviderType = @LockProviderType,
                LockConfigJson = @LockConfigJson,
                KeypadProviderType = @KeypadProviderType,
                KeypadConfigJson = @KeypadConfigJson,
                DoorSensorProviderType = @DoorSensorProviderType,
                DoorSensorConfigJson = @DoorSensorConfigJson,
                AutoLockEnabled = @AutoLockEnabled,
                AutoLockDaySeconds = @AutoLockDaySeconds,
                AutoLockNightSeconds = @AutoLockNightSeconds,
                RetryOnFailure = @RetryOnFailure,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id;";

        var parameters = new
        {
            accessPoint.Id,
            accessPoint.Name,
            accessPoint.LockProviderType,
            LockConfigJson = string.IsNullOrWhiteSpace(accessPoint.LockConfigJson) ? "{}" : accessPoint.LockConfigJson,
            KeypadProviderType = accessPoint.KeypadProviderType ?? "None",
            accessPoint.KeypadConfigJson,
            DoorSensorProviderType = accessPoint.DoorSensorProviderType ?? "None",
            accessPoint.DoorSensorConfigJson,
            AutoLockEnabled = accessPoint.AutoLockEnabled ? 1 : 0,
            accessPoint.AutoLockDaySeconds,
            accessPoint.AutoLockNightSeconds,
            RetryOnFailure = accessPoint.RetryOnFailure ? 1 : 0,
            UpdatedAt = accessPoint.UpdatedAt.ToString("O")
        };

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM AccessPoints WHERE Id = @Id;";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }
}
