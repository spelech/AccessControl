using AccessControl.Core.Models;
using AccessControl.Data.Db;
using AccessControl.Data.Repositories;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AccessControl.Tests.Harness.Api;

public sealed class AccessPointCreationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly AccessPointRepository _repository;
    private bool _disposed;

    public AccessPointCreationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"accesscontrol_ap_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_dbPath}");

        var seeder = new DatabaseSeederService(_connectionFactory, NullLogger<DatabaseSeederService>.Instance);
        seeder.InitializeAsync().GetAwaiter().GetResult();

        _repository = new AccessPointRepository(_connectionFactory);
    }

    [Fact]
    public async Task InsertAccessPoint_WithProgressiveDisclosurePayload_PersistsAndQueriesCorrectlyViaDapper()
    {
        // Arrange
        var newDoor = new AccessPoint
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Front Entryway",
            LockProviderType = "ZWaveWebSocket",
            LockConfigJson = "{\"nodeId\":39}",
            KeypadProviderType = "BuiltInKeypad",
            KeypadConfigJson = "{\"nodeId\":39}",
            DoorSensorProviderType = "AqaraZigbee",
            DoorSensorConfigJson = "{\"topic\":\"zigbee2mqtt/front_door_contact\"}",
            AutoLockEnabled = true,
            AutoLockDaySeconds = 300,
            AutoLockNightSeconds = 60,
            RetryOnFailure = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Insert via repository
        await _repository.InsertAsync(newDoor);

        // Assert - Query back directly via Dapper
        using var connection = _connectionFactory.CreateConnection();
        var retrieved = connection.QuerySingle<AccessPoint>(
            "SELECT * FROM AccessPoints WHERE Name = @Name",
            new { Name = "Front Entryway" });

        Assert.NotNull(retrieved);
        Assert.Equal(newDoor.Id, retrieved.Id);
        Assert.Equal("Front Entryway", retrieved.Name);
        Assert.Equal("ZWaveWebSocket", retrieved.LockProviderType);
        Assert.Equal("{\"nodeId\":39}", retrieved.LockConfigJson);
        Assert.Equal("BuiltInKeypad", retrieved.KeypadProviderType);
        Assert.Equal("{\"nodeId\":39}", retrieved.KeypadConfigJson);
        Assert.Equal("AqaraZigbee", retrieved.DoorSensorProviderType);
        Assert.Equal("{\"topic\":\"zigbee2mqtt/front_door_contact\"}", retrieved.DoorSensorConfigJson);
        Assert.True(retrieved.AutoLockEnabled);
        Assert.Equal(300, retrieved.AutoLockDaySeconds);
        Assert.Equal(60, retrieved.AutoLockNightSeconds);
        Assert.True(retrieved.RetryOnFailure);
    }

    [Fact]
    public async Task InsertAccessPoint_DirectDapperInsert_CanBeRetrievedByRepository()
    {
        // Arrange
        var doorId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        using (var connection = _connectionFactory.CreateConnection())
        {
            const string insertSql = @"
                INSERT INTO AccessPoints (
                    Id, Name, LockProviderType, LockConfigJson,
                    KeypadProviderType, KeypadConfigJson,
                    DoorSensorProviderType, DoorSensorConfigJson,
                    AutoLockEnabled, AutoLockDaySeconds, AutoLockNightSeconds,
                    RetryOnFailure, CreatedAt, UpdatedAt
                ) VALUES (
                    @Id, @Name, @LockProviderType, @LockConfigJson,
                    @KeypadProviderType, @KeypadConfigJson,
                    @DoorSensorProviderType, @DoorSensorConfigJson,
                    @AutoLockEnabled, @AutoLockDaySeconds, @AutoLockNightSeconds,
                    @RetryOnFailure, @CreatedAt, @UpdatedAt
                );";

            var parameters = new
            {
                Id = doorId,
                Name = "Patio Gate",
                LockProviderType = "GenericMqtt",
                LockConfigJson = "{\"topic\":\"zwave/patio_gate\"}",
                KeypadProviderType = "RingMqtt",
                KeypadConfigJson = "{\"topic\":\"ring/keypad_patio\"}",
                DoorSensorProviderType = "GenericMqttContact",
                DoorSensorConfigJson = "{\"topic\":\"zigbee2mqtt/patio_contact\"}",
                AutoLockEnabled = 0,
                AutoLockDaySeconds = 120,
                AutoLockNightSeconds = 30,
                RetryOnFailure = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            await connection.ExecuteAsync(insertSql, parameters);
        }

        // Act - Retrieve via repository
        var retrieved = await _repository.GetByIdAsync(doorId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(doorId, retrieved.Id);
        Assert.Equal("Patio Gate", retrieved.Name);
        Assert.Equal("GenericMqtt", retrieved.LockProviderType);
        Assert.Equal("{\"topic\":\"zwave/patio_gate\"}", retrieved.LockConfigJson);
        Assert.Equal("RingMqtt", retrieved.KeypadProviderType);
        Assert.Equal("{\"topic\":\"ring/keypad_patio\"}", retrieved.KeypadConfigJson);
        Assert.Equal("GenericMqttContact", retrieved.DoorSensorProviderType);
        Assert.Equal("{\"topic\":\"zigbee2mqtt/patio_contact\"}", retrieved.DoorSensorConfigJson);
        Assert.False(retrieved.AutoLockEnabled);
        Assert.Equal(120, retrieved.AutoLockDaySeconds);
        Assert.Equal(30, retrieved.AutoLockNightSeconds);
        Assert.False(retrieved.RetryOnFailure);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
            var wal = _dbPath + "-wal";
            if (File.Exists(wal)) File.Delete(wal);
            var shm = _dbPath + "-shm";
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch
        {
            // Ignore temp file cleanup exceptions
        }
    }
}
