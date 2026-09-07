using AccessControl.Core.Models;
using AccessControl.Data.Db;
using Dapper;

namespace AccessControl.Data.Repositories;

public class HardwareSlotRepository : IHardwareSlotRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public HardwareSlotRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<HardwareSlot?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, AccessPointId, SlotNumber, UserId, CredentialId, SyncStatus, LastSyncedAt
            FROM HardwareSlots
            WHERE Id = @Id;";

        return await connection.QuerySingleOrDefaultAsync<HardwareSlot>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<HardwareSlot?> GetSlotAsync(string accessPointId, int slotNumber, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, AccessPointId, SlotNumber, UserId, CredentialId, SyncStatus, LastSyncedAt
            FROM HardwareSlots
            WHERE AccessPointId = @AccessPointId AND SlotNumber = @SlotNumber;";

        return await connection.QuerySingleOrDefaultAsync<HardwareSlot>(new CommandDefinition(sql, new { AccessPointId = accessPointId, SlotNumber = slotNumber }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<HardwareSlot>> GetSlotsForDoorAsync(string accessPointId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, AccessPointId, SlotNumber, UserId, CredentialId, SyncStatus, LastSyncedAt
            FROM HardwareSlots
            WHERE AccessPointId = @AccessPointId
            ORDER BY SlotNumber ASC;";

        var results = await connection.QueryAsync<HardwareSlot>(new CommandDefinition(sql, new { AccessPointId = accessPointId }, cancellationToken: ct));
        return results.ToList();
    }

    public async Task UpdateSlotSyncStatusAsync(string slotId, SlotSyncStatus status, DateTime? lastSyncedAt = null, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE HardwareSlots
            SET SyncStatus = @SyncStatus,
                LastSyncedAt = @LastSyncedAt
            WHERE Id = @Id;";

        var parameters = new
        {
            Id = slotId,
            SyncStatus = status.ToString(),
            LastSyncedAt = lastSyncedAt?.ToString("O") ?? DateTime.UtcNow.ToString("O")
        };

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task<HardwareSlot?> AllocateSlotAsync(string accessPointId, string userId, string credentialId, int slotNumber, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        const string sql = @"
            INSERT INTO HardwareSlots (Id, AccessPointId, SlotNumber, UserId, CredentialId, SyncStatus, LastSyncedAt)
            VALUES (@Id, @AccessPointId, @SlotNumber, @UserId, @CredentialId, @SyncStatus, @LastSyncedAt)
            ON CONFLICT(AccessPointId, SlotNumber) DO UPDATE SET
                UserId = excluded.UserId,
                CredentialId = excluded.CredentialId,
                SyncStatus = excluded.SyncStatus,
                LastSyncedAt = excluded.LastSyncedAt;";

        var parameters = new
        {
            Id = id,
            AccessPointId = accessPointId,
            SlotNumber = slotNumber,
            UserId = userId,
            CredentialId = credentialId,
            SyncStatus = SlotSyncStatus.Synced.ToString(),
            LastSyncedAt = now.ToString("O")
        };

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
        return await GetSlotAsync(accessPointId, slotNumber, ct);
    }

    public async Task ClearSlotAsync(string slotId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE HardwareSlots
            SET UserId = NULL,
                CredentialId = NULL,
                SyncStatus = 'Synced',
                LastSyncedAt = @LastSyncedAt
            WHERE Id = @Id;";

        var parameters = new
        {
            Id = slotId,
            LastSyncedAt = DateTime.UtcNow.ToString("O")
        };

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task DeleteSlotAsync(string slotId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM HardwareSlots WHERE Id = @Id;";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = slotId }, cancellationToken: ct));
    }
}
