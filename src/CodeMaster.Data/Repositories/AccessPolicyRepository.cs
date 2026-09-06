using CodeMaster.Core.Models;
using CodeMaster.Data.Db;
using Dapper;

namespace CodeMaster.Data.Repositories;

public class AccessPolicyRepository : IAccessPolicyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AccessPolicyRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<AccessPolicy?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, Name, ScheduleType, DaysOfWeek, StartTime, EndTime,
                   ValidFrom, ValidUntil, RemainingUses, IsEnabled, TimeZoneId
            FROM AccessPolicies
            WHERE Id = @Id;";

        return await connection.QuerySingleOrDefaultAsync<AccessPolicy>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AccessPolicy>> GetByAccessPointIdAsync(string accessPointId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT p.Id, p.Name, p.ScheduleType, p.DaysOfWeek, p.StartTime, p.EndTime,
                   p.ValidFrom, p.ValidUntil, p.RemainingUses, p.IsEnabled, p.TimeZoneId
            FROM AccessPolicies p
            INNER JOIN AccessAssignments a ON p.Id = a.PolicyId
            WHERE a.AccessPointId = @AccessPointId;";

        var results = await connection.QueryAsync<AccessPolicy>(new CommandDefinition(sql, new { AccessPointId = accessPointId }, cancellationToken: ct));
        return results.ToList();
    }

    public async Task<IReadOnlyList<AccessPolicy>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT DISTINCT p.Id, p.Name, p.ScheduleType, p.DaysOfWeek, p.StartTime, p.EndTime,
                            p.ValidFrom, p.ValidUntil, p.RemainingUses, p.IsEnabled, p.TimeZoneId
            FROM AccessPolicies p
            INNER JOIN AccessAssignments a ON p.Id = a.PolicyId
            LEFT JOIN Users u ON u.Id = @UserId
            WHERE a.UserId = @UserId OR (a.GroupId IS NOT NULL AND a.GroupId = u.GroupId);";

        var results = await connection.QueryAsync<AccessPolicy>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));
        return results.ToList();
    }

    public async Task InsertAsync(AccessPolicy policy, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO AccessPolicies (
                Id, Name, ScheduleType, DaysOfWeek, StartTime, EndTime,
                ValidFrom, ValidUntil, RemainingUses, IsEnabled, TimeZoneId
            )
            VALUES (
                @Id, @Name, @ScheduleType, @DaysOfWeek, @StartTime, @EndTime,
                @ValidFrom, @ValidUntil, @RemainingUses, @IsEnabled, @TimeZoneId
            );";

        var parameters = new
        {
            policy.Id,
            policy.Name,
            ScheduleType = policy.ScheduleType.ToString(),
            policy.DaysOfWeek,
            StartTime = policy.StartTime?.ToString("HH:mm:ss"),
            EndTime = policy.EndTime?.ToString("HH:mm:ss"),
            ValidFrom = policy.ValidFrom?.ToString("O"),
            ValidUntil = policy.ValidUntil?.ToString("O"),
            policy.RemainingUses,
            IsEnabled = policy.IsEnabled ? 1 : 0,
            policy.TimeZoneId
        };

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task UpdateAsync(AccessPolicy policy, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE AccessPolicies
            SET Name = @Name,
                ScheduleType = @ScheduleType,
                DaysOfWeek = @DaysOfWeek,
                StartTime = @StartTime,
                EndTime = @EndTime,
                ValidFrom = @ValidFrom,
                ValidUntil = @ValidUntil,
                RemainingUses = @RemainingUses,
                IsEnabled = @IsEnabled,
                TimeZoneId = @TimeZoneId
            WHERE Id = @Id;";

        var parameters = new
        {
            policy.Id,
            policy.Name,
            ScheduleType = policy.ScheduleType.ToString(),
            policy.DaysOfWeek,
            StartTime = policy.StartTime?.ToString("HH:mm:ss"),
            EndTime = policy.EndTime?.ToString("HH:mm:ss"),
            ValidFrom = policy.ValidFrom?.ToString("O"),
            ValidUntil = policy.ValidUntil?.ToString("O"),
            policy.RemainingUses,
            IsEnabled = policy.IsEnabled ? 1 : 0,
            policy.TimeZoneId
        };

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM AccessPolicies WHERE Id = @Id;";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task AssignPolicyAsync(AccessAssignment assignment, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO AccessAssignments (Id, AccessPointId, UserId, GroupId, PolicyId)
            VALUES (@Id, @AccessPointId, @UserId, @GroupId, @PolicyId);";

        await connection.ExecuteAsync(new CommandDefinition(sql, assignment, cancellationToken: ct));
    }

    public async Task RemoveAssignmentAsync(string assignmentId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM AccessAssignments WHERE Id = @Id;";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = assignmentId }, cancellationToken: ct));
    }
}
