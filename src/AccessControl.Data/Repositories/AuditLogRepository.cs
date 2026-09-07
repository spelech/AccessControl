using AccessControl.Core.Models;
using AccessControl.Data.Db;
using Dapper;

namespace AccessControl.Data.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task InsertAsync(AccessLog log, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO AccessLogs (
                Id, AccessPointId, UserId, UserName, CredentialType,
                EventType, Method, Timestamp, Details
            )
            VALUES (
                @Id, @AccessPointId, @UserId, @UserName, @CredentialType,
                @EventType, @Method, @Timestamp, @Details
            );";

        var parameters = new
        {
            log.Id,
            log.AccessPointId,
            log.UserId,
            log.UserName,
            CredentialType = log.CredentialType?.ToString() ?? "PIN",
            EventType = log.EventType.ToString(),
            Method = log.Method.ToString(),
            Timestamp = log.Timestamp.ToString("O"),
            log.Details
        };

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AccessLog>> GetRecentLogsAsync(string accessPointId, int limit = 50, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, AccessPointId, UserId, UserName, CredentialType,
                   EventType, Method, Timestamp, Details
            FROM AccessLogs
            WHERE AccessPointId = @AccessPointId
            ORDER BY Timestamp DESC
            LIMIT @Limit;";

        var results = await connection.QueryAsync<AccessLog>(new CommandDefinition(sql, new { AccessPointId = accessPointId, Limit = limit }, cancellationToken: ct));
        return results.ToList();
    }

    public async Task<IReadOnlyList<AccessLog>> GetAllRecentLogsAsync(int limit = 100, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, AccessPointId, UserId, UserName, CredentialType,
                   EventType, Method, Timestamp, Details
            FROM AccessLogs
            ORDER BY Timestamp DESC
            LIMIT @Limit;";

        var results = await connection.QueryAsync<AccessLog>(new CommandDefinition(sql, new { Limit = limit }, cancellationToken: ct));
        return results.ToList();
    }
}
