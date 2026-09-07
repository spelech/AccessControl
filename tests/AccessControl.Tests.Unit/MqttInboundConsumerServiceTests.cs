using System.Text.Json;
using AccessControl.Core.DTOs;
using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;
using AccessControl.Data.Db;
using AccessControl.Data.Repositories;
using AccessControl.Engine.Channels;
using AccessControl.Engine.Mqtt;
using AccessControl.Engine.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AccessControl.Tests.Unit;

public sealed class MqttInboundConsumerServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ServiceProvider _serviceProvider;
    private readonly IMqttInboundChannel _inboundChannel;
    private readonly MqttInboundConsumerService _consumerService;

    public MqttInboundConsumerServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"codemaster_consumer_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_dbPath}");

        var seeder = new DatabaseSeederService(_connectionFactory, NullLogger<DatabaseSeederService>.Instance);
        seeder.InitializeAsync().GetAwaiter().GetResult();

        var services = new ServiceCollection();
        services.AddSingleton<IDbConnectionFactory>(_connectionFactory);
        services.AddScoped<IAccessPointRepository, AccessPointRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICredentialRepository, CredentialRepository>();
        services.AddScoped<IAccessPolicyRepository, AccessPolicyRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IHardwareSlotRepository, HardwareSlotRepository>();

        services.AddSingleton<IMqttInboundChannel, MqttInboundChannel>();
        services.AddScoped<IDoorOperationService, DoorOperationService>();
        services.AddScoped<IAccessPolicyEvaluator, AccessPolicyEvaluator>();
        services.AddSingleton<IMqttDiscoveryService, MqttTopicDiscoveryService>();
        services.AddSingleton<IHomeAssistantDiscoveryService, HomeAssistantDiscoveryService>();
        services.AddSingleton<IAccessEventBroadcaster, AccessEventBroadcaster>();
        services.AddSingleton<ILogger<MqttInboundConsumerService>>(NullLogger<MqttInboundConsumerService>.Instance);
        services.AddSingleton<ILogger<DoorOperationService>>(NullLogger<DoorOperationService>.Instance);

        _serviceProvider = services.BuildServiceProvider();
        _inboundChannel = _serviceProvider.GetRequiredService<IMqttInboundChannel>();

        _consumerService = new MqttInboundConsumerService(
            _inboundChannel,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<IMqttDiscoveryService>(),
            _serviceProvider.GetRequiredService<ILogger<MqttInboundConsumerService>>());
    }

    [Fact]
    public async Task KeypadMessage_DispatchedThroughInboundChannel_UnlocksDoorAndRecordsAuditLog()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // 1. Seed AccessPoint, User, PIN Credential, and AccessPolicy
        using (var scope = _serviceProvider.CreateScope())
        {
            var doorRepo = scope.ServiceProvider.GetRequiredService<IAccessPointRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var credRepo = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();
            var policyRepo = scope.ServiceProvider.GetRequiredService<IAccessPolicyRepository>();

            var door = new AccessPoint
            {
                Id = "front_door_1",
                Name = "Front Door",
                LockProviderType = "GenericMqtt",
                KeypadProviderType = "RingKeypad",
                KeypadConfigJson = "{\"topic\":\"ring/keypad\"}",
                AutoLockEnabled = true,
                AutoLockDaySeconds = 120
            };
            await doorRepo.InsertAsync(door, cts.Token);

            var user = new User { Id = "user_1", Name = "Steven", Role = UserRole.Admin, IsActive = true };
            await userRepo.InsertAsync(user, cts.Token);

            var cred = new Credential
            {
                Id = "cred_1",
                UserId = "user_1",
                Type = CredentialType.PIN,
                EncryptedValue = "1234",
                HashedValue = AccessPolicyEvaluator.ComputeSha256Hex("1234"),
                PinLength = 4
            };
            await credRepo.InsertAsync(cred, cts.Token);

            var policy = new AccessPolicy
            {
                Id = "policy_1",
                Name = "24/7 Access",
                ScheduleType = ScheduleType.Always,
                IsEnabled = true
            };
            await policyRepo.InsertAsync(policy, cts.Token);

            await policyRepo.AssignPolicyAsync(new AccessAssignment
            {
                AccessPointId = "front_door_1",
                UserId = "user_1",
                PolicyId = "policy_1"
            }, cts.Token);
        }

        // 2. Publish Keypad Disarm Message into IMqttInboundChannel
        var keypadPayload = JsonSerializer.Serialize(new
        {
            command = "disarm",
            code = "1234",
            device_id = "ring/keypad"
        });
        var message = new MqttInboundMessage("ring/keypad", keypadPayload, DateTimeOffset.UtcNow);

        await _consumerService.ProcessMessageAsync(message, cts.Token);

        // 3. Verify door state is Unlocked and an audit log was written
        using (var scope = _serviceProvider.CreateScope())
        {
            var doorOps = scope.ServiceProvider.GetRequiredService<IDoorOperationService>();
            var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

            var lockState = await doorOps.GetDoorLockStateAsync("front_door_1", cts.Token);
            Assert.Equal(LockState.Unlocked, lockState);

            var logs = await auditRepo.GetRecentLogsAsync("front_door_1", 10, cts.Token);
            Assert.NotEmpty(logs);
            var lastLog = logs[0];
            Assert.Equal(AccessEventType.Unlocked, lastLog.EventType);
            Assert.Equal("Steven", lastLog.UserName);
            Assert.Equal(AccessMethod.RingKeypad, lastLog.Method);
        }
    }

    [Fact]
    public async Task LockAndSensorTelemetry_UpdatesDoorStatesAndAutoLockCountdown()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using (var scope = _serviceProvider.CreateScope())
        {
            var doorRepo = scope.ServiceProvider.GetRequiredService<IAccessPointRepository>();
            var door = new AccessPoint
            {
                Id = "patio_door",
                Name = "Patio Door",
                LockProviderType = "GenericMqtt",
                DoorSensorProviderType = "MqttContact",
                DoorSensorConfigJson = "{\"topic\":\"zigbee/patio_contact\"}",
                AutoLockEnabled = true,
                AutoLockDaySeconds = 90
            };
            await doorRepo.InsertAsync(door, cts.Token);
        }

        // 1. Process contact open message
        await _consumerService.ProcessMessageAsync(
            new MqttInboundMessage("zigbee/patio_contact", "{\"contact\":false,\"state\":\"ON\"}"),
            cts.Token);

        // 2. Process lock state unlocked message
        await _consumerService.ProcessMessageAsync(
            new MqttInboundMessage("codemaster/patio_door/lock/state", "unlocked"),
            cts.Token);

        using (var scope = _serviceProvider.CreateScope())
        {
            var doorOps = scope.ServiceProvider.GetRequiredService<IDoorOperationService>();
            var contactState = await doorOps.GetDoorContactStateAsync("patio_door", cts.Token);
            var lockState = await doorOps.GetDoorLockStateAsync("patio_door", cts.Token);

            Assert.Equal(DoorContactState.Open, contactState);
            Assert.Equal(LockState.Unlocked, lockState);
        }

        // 3. Process contact closed message -> triggers countdown arming
        await _consumerService.ProcessMessageAsync(
            new MqttInboundMessage("zigbee/patio_contact", "{\"contact\":true,\"state\":\"OFF\"}"),
            cts.Token);

        using (var scope = _serviceProvider.CreateScope())
        {
            var doorOps = scope.ServiceProvider.GetRequiredService<IDoorOperationService>();
            var contactState = await doorOps.GetDoorContactStateAsync("patio_door", cts.Token);
            var remaining = await doorOps.GetRemainingAutoLockSecondsAsync("patio_door", cts.Token);

            Assert.Equal(DoorContactState.Closed, contactState);
            Assert.NotNull(remaining);
            Assert.True(remaining > 0 && remaining <= 90);
        }
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
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
            // Ignore temp cleanup errors
        }
    }
}
