using System.Data;
using CodeMaster.Core.Models;
using CodeMaster.Data.Db;
using CodeMaster.Data.Repositories;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodeMaster.Tests.Unit;

public sealed class SqliteTestConnectionFactory : IDbConnectionFactory, IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _innerFactory;

    public SqliteTestConnectionFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"codemaster_test_{Guid.NewGuid():N}.db");
        _innerFactory = new SqliteConnectionFactory($"Data Source={_dbPath}");
    }

    public IDbConnection CreateConnection() => _innerFactory.CreateConnection();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
        var wal = _dbPath + "-wal";
        if (File.Exists(wal))
        {
            try { File.Delete(wal); } catch { }
        }
        var shm = _dbPath + "-shm";
        if (File.Exists(shm))
        {
            try { File.Delete(shm); } catch { }
        }
    }
}

public class DatabaseSeederTests
{
    [Fact]
    public async Task Seeder_InitializesDatabase_TablesExist()
    {
        using var factory = new SqliteTestConnectionFactory();
        var seeder = new DatabaseSeederService(factory, NullLogger<DatabaseSeederService>.Instance);
        await seeder.InitializeAsync(CancellationToken.None);

        using var conn = factory.CreateConnection();
        var tables = (await conn.QueryAsync<string>("SELECT name FROM sqlite_master WHERE type='table';")).ToList();

        Assert.Contains("UserGroups", tables);
        Assert.Contains("Users", tables);
        Assert.Contains("Credentials", tables);
        Assert.Contains("AccessPoints", tables);
        Assert.Contains("AccessPolicies", tables);
        Assert.Contains("AccessAssignments", tables);
        Assert.Contains("HardwareSlots", tables);
        Assert.Contains("AccessLogs", tables);
    }

    [Fact]
    public async Task Repositories_PerformCrudOperationsSuccessfully()
    {
        using var factory = new SqliteTestConnectionFactory();
        var seeder = new DatabaseSeederService(factory, NullLogger<DatabaseSeederService>.Instance);
        await seeder.InitializeAsync(CancellationToken.None);

        var userRepo = new UserRepository(factory);
        var credRepo = new CredentialRepository(factory);
        var apRepo = new AccessPointRepository(factory);
        var policyRepo = new AccessPolicyRepository(factory);
        var slotRepo = new HardwareSlotRepository(factory);
        var logRepo = new AuditLogRepository(factory);

        // 1. User CRUD
        var user = new User
        {
            Id = "usr_test_1",
            Name = "John Doe",
            Role = UserRole.Admin,
            IsActive = true
        };
        await userRepo.InsertAsync(user);

        var retrievedUser = await userRepo.GetByIdAsync(user.Id);
        Assert.NotNull(retrievedUser);
        Assert.Equal("John Doe", retrievedUser.Name);
        Assert.Equal(UserRole.Admin, retrievedUser.Role);

        user.Name = "Johnathan Doe";
        await userRepo.UpdateAsync(user);
        var updatedUser = await userRepo.GetByIdAsync(user.Id);
        Assert.Equal("Johnathan Doe", updatedUser?.Name);

        var allUsers = await userRepo.GetAllAsync();
        Assert.Contains(allUsers, u => u.Id == user.Id);

        // 2. Credential CRUD
        var cred = new Credential
        {
            Id = "cred_test_1",
            UserId = user.Id,
            Type = CredentialType.PIN,
            EncryptedValue = "enc_1234",
            HashedValue = "hash_1234",
            PinLength = 4,
            Label = "Master PIN"
        };
        await credRepo.InsertAsync(cred);

        var userCreds = await credRepo.GetByUserIdAsync(user.Id);
        Assert.Single(userCreds);
        Assert.Equal("Master PIN", userCreds[0].Label);
        Assert.Equal(CredentialType.PIN, userCreds[0].Type);

        // 3. AccessPoint CRUD
        var ap = new AccessPoint
        {
            Id = "ap_front_door",
            Name = "Front Door",
            LockProviderType = "ZWaveJsMqtt",
            LockConfigJson = "{\"nodeId\":5}",
            KeypadProviderType = "RingMqtt",
            KeypadConfigJson = "{\"topic\":\"ring/keypad\"}",
            AutoLockEnabled = true,
            AutoLockDaySeconds = 180,
            AutoLockNightSeconds = 60,
            RetryOnFailure = true
        };
        await apRepo.InsertAsync(ap);

        var retrievedAp = await apRepo.GetByIdAsync(ap.Id);
        Assert.NotNull(retrievedAp);
        Assert.Equal("Front Door", retrievedAp.Name);
        Assert.Equal("ZWaveJsMqtt", retrievedAp.LockProviderType);

        ap.Name = "Front Door Main";
        await apRepo.UpdateAsync(ap);
        var updatedAp = await apRepo.GetByIdAsync(ap.Id);
        Assert.Equal("Front Door Main", updatedAp?.Name);

        // 4. AccessPolicy CRUD & Assignments
        var policy = new AccessPolicy
        {
            Id = "policy_247",
            Name = "Always 24/7",
            ScheduleType = ScheduleType.Always,
            DaysOfWeek = 127,
            IsEnabled = true
        };
        await policyRepo.InsertAsync(policy);

        var retrievedPolicy = await policyRepo.GetByIdAsync(policy.Id);
        Assert.NotNull(retrievedPolicy);
        Assert.Equal(ScheduleType.Always, retrievedPolicy.ScheduleType);

        // Assign policy to user & access point
        await policyRepo.AssignPolicyAsync(new AccessAssignment
        {
            Id = "assign_1",
            AccessPointId = ap.Id,
            UserId = user.Id,
            PolicyId = policy.Id
        });

        var policiesForAp = await policyRepo.GetByAccessPointIdAsync(ap.Id);
        Assert.Contains(policiesForAp, p => p.Id == policy.Id);

        var policiesForUser = await policyRepo.GetByUserIdAsync(user.Id);
        Assert.Contains(policiesForUser, p => p.Id == policy.Id);

        // 5. HardwareSlot Operations
        var allocatedSlot = await slotRepo.AllocateSlotAsync(ap.Id, user.Id, cred.Id, 1);
        Assert.NotNull(allocatedSlot);
        Assert.Equal(1, allocatedSlot.SlotNumber);
        Assert.Equal(SlotSyncStatus.Synced, allocatedSlot.SyncStatus);

        var doorSlots = await slotRepo.GetSlotsForDoorAsync(ap.Id);
        Assert.Single(doorSlots);
        Assert.Equal(1, doorSlots[0].SlotNumber);

        await slotRepo.UpdateSlotSyncStatusAsync(allocatedSlot.Id, SlotSyncStatus.Adding);
        doorSlots = await slotRepo.GetSlotsForDoorAsync(ap.Id);
        Assert.Equal(SlotSyncStatus.Adding, doorSlots[0].SyncStatus);

        await slotRepo.ClearSlotAsync(allocatedSlot.Id);
        doorSlots = await slotRepo.GetSlotsForDoorAsync(ap.Id);
        Assert.Null(doorSlots[0].UserId);
        Assert.Null(doorSlots[0].CredentialId);

        // 6. AuditLog Operations
        var log = new AccessLog
        {
            Id = "log_1",
            AccessPointId = ap.Id,
            UserId = user.Id,
            UserName = user.Name,
            CredentialType = CredentialType.PIN,
            EventType = AccessEventType.Unlocked,
            Method = AccessMethod.RingKeypad,
            Timestamp = DateTime.UtcNow,
            Details = "Disarmed via Ring Keypad"
        };
        await logRepo.InsertAsync(log);

        var recentLogs = await logRepo.GetRecentLogsAsync(ap.Id, 10);
        Assert.NotEmpty(recentLogs);
        Assert.Equal("Front Door Main", updatedAp?.Name);
        Assert.Equal(AccessEventType.Unlocked, recentLogs[0].EventType);
        Assert.Equal("RingKeypad", recentLogs[0].Method.ToString());

        // 7. Delete operations
        await credRepo.DeleteAsync(cred.Id);
        var credsAfterDelete = await credRepo.GetByUserIdAsync(user.Id);
        Assert.Empty(credsAfterDelete);

        await userRepo.DeleteAsync(user.Id);
        var userAfterDelete = await userRepo.GetByIdAsync(user.Id);
        Assert.Null(userAfterDelete);

        await apRepo.DeleteAsync(ap.Id);
        var apAfterDelete = await apRepo.GetByIdAsync(ap.Id);
        Assert.Null(apAfterDelete);
    }
}
