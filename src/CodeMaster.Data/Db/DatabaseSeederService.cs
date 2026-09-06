using System.Reflection;
using Dapper;
using Microsoft.Extensions.Logging;

namespace CodeMaster.Data.Db;

public class DatabaseSeederService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseSeederService> _logger;

    public DatabaseSeederService(IDbConnectionFactory connectionFactory, ILogger<DatabaseSeederService> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Initializing database schema...");

        var sql = await LoadInitSchemaSqlAsync(ct);
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new InvalidOperationException("InitSchema.sql could not be found or is empty.");
        }

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("ALTER TABLE AccessPolicies ADD COLUMN TimeZoneId TEXT;", cancellationToken: ct));
        }
        catch
        {
            // Column already exists or table was just created with column
        }

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(@"
                CREATE TABLE IF NOT EXISTS system_settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL,
                    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
                );", cancellationToken: ct));
        }
        catch
        {
            // Table already exists
        }

        _logger.LogInformation("Database schema initialized successfully.");
    }

    private static async Task<string> LoadInitSchemaSqlAsync(CancellationToken ct)
    {
        var assembly = typeof(DatabaseSeederService).Assembly;
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("InitSchema.sql", StringComparison.OrdinalIgnoreCase));
        if (resourceName != null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync(ct);
            }
        }

        // Fallback to filesystem
        var pathsToTry = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Scripts", "InitSchema.sql"),
            Path.Combine(AppContext.BaseDirectory, "InitSchema.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "CodeMaster.Data", "Scripts", "InitSchema.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "InitSchema.sql")
        };

        foreach (var path in pathsToTry)
        {
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path, ct);
            }
        }

        throw new FileNotFoundException("Unable to locate InitSchema.sql as an embedded resource or on disk.");
    }
}
