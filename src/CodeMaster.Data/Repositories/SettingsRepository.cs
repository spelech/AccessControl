using CodeMaster.Core.Interfaces;
using CodeMaster.Data.Db;
using Dapper;

namespace CodeMaster.Data.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SettingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllSettingsAsync(CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT key, value FROM system_settings;";
        var rows = await connection.QueryAsync<(string Key, string Value)>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT value FROM system_settings WHERE key = @Key;";
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(sql, new { Key = key }, cancellationToken: ct));
    }

    public async Task SetSettingAsync(string key, string value, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO system_settings (key, value, updated_at)
            VALUES (@Key, @Value, datetime('now'))
            ON CONFLICT(key) DO UPDATE SET value = @Value, updated_at = datetime('now');";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Key = key, Value = value }, cancellationToken: ct));
    }

    public async Task SetSettingsAsync(IDictionary<string, string> settings, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }
        using var transaction = connection.BeginTransaction();
        const string sql = @"
            INSERT INTO system_settings (key, value, updated_at)
            VALUES (@Key, @Value, datetime('now'))
            ON CONFLICT(key) DO UPDATE SET value = @Value, updated_at = datetime('now');";

        foreach (var kvp in settings)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, new { Key = kvp.Key, Value = kvp.Value }, transaction: transaction, cancellationToken: ct));
        }

        transaction.Commit();
    }
}
