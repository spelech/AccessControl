using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using AccessControl.Core.DTOs;
using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;
using AccessControl.Data.Db;
using AccessControl.Data.Repositories;
using AccessControl.Engine.Providers.Keypads;
using AccessControl.Engine.Providers.Locks;
using AccessControl.Engine.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccessControl.Tests.Harness;

public sealed class ControlsTestHarness : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ConcurrentDictionary<string, DoorContext> _doors = new();
    private readonly List<IDisposable> _subscriptions = new();
    private bool _disposed;

    public MockMqttBroker Broker { get; }
    public IUserRepository UserRepository { get; }
    public ICredentialRepository CredentialRepository { get; }
    public IAccessPointRepository AccessPointRepository { get; }
    public IAccessPolicyRepository AccessPolicyRepository { get; }
    public IHardwareSlotRepository HardwareSlotRepository { get; }
    public IAuditLogRepository AuditLogRepository { get; }
    public IAccessPolicyEvaluator AccessPolicyEvaluator { get; }
    public IHardwareSlotSyncWorker HardwareSlotSyncWorker { get; }

    public ControlsTestHarness()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"accesscontrol_harness_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_dbPath}");

        var seeder = new DatabaseSeederService(_connectionFactory, NullLogger<DatabaseSeederService>.Instance);
        seeder.InitializeAsync().GetAwaiter().GetResult();

        UserRepository = new UserRepository(_connectionFactory);
        CredentialRepository = new CredentialRepository(_connectionFactory);
        AccessPointRepository = new AccessPointRepository(_connectionFactory);
        AccessPolicyRepository = new AccessPolicyRepository(_connectionFactory);
        HardwareSlotRepository = new HardwareSlotRepository(_connectionFactory);
        AuditLogRepository = new AuditLogRepository(_connectionFactory);

        Broker = new MockMqttBroker();
        AccessPolicyEvaluator = new AccessPolicyEvaluator(
            AccessPolicyRepository,
            UserRepository,
            CredentialRepository,
            NullLogger<AccessPolicyEvaluator>.Instance);

        HardwareSlotSyncWorker = new HardwareSlotSyncWorker(
            HardwareSlotRepository,
            UserRepository,
            CredentialRepository,
            AccessPolicyRepository,
            NullLogger<HardwareSlotSyncWorker>.Instance);
    }

    public async Task<AccessPoint> SetupDoorAsync(
        string doorId,
        string lockTopic,
        string keypadTopic,
        bool autoLockEnabled = true,
        int daySeconds = 60,
        int nightSeconds = 60,
        bool retryOnFailure = true)
    {
        var ap = new AccessPoint
        {
            Id = doorId,
            Name = doorId,
            LockProviderType = "ZWaveJsMqtt",
            LockConfigJson = JsonSerializer.Serialize(new { topic = lockTopic, nodeId = lockTopic }),
            KeypadProviderType = "RingMqtt",
            KeypadConfigJson = JsonSerializer.Serialize(new { topic = keypadTopic }),
            AutoLockEnabled = autoLockEnabled,
            AutoLockDaySeconds = daySeconds,
            AutoLockNightSeconds = nightSeconds,
            RetryOnFailure = retryOnFailure
        };

        await AccessPointRepository.InsertAsync(ap);

        var lockProvider = new ZWaveJsMqttLockProvider(lockTopic, 0, Broker);
        var keypadProvider = new RingMqttKeypadProvider(keypadTopic, Broker);

        AutoLockStateMachine? autoLockSm = null;
        autoLockSm = new AutoLockStateMachine(
            timeoutSeconds: daySeconds,
            nightSeconds: nightSeconds,
            retryOnFailure: retryOnFailure,
            retryDelaySeconds: 15,
            onLockRequested: async () =>
            {
                return await lockProvider.LockAsync();
            });
        autoLockSm.IsEnabled = autoLockEnabled;

        var context = new DoorContext(ap, lockProvider, keypadProvider, autoLockSm, lockTopic, keypadTopic);
        _doors[doorId] = context;

        // Wire broker subscription for keypad topic
        var sub = Broker.Subscribe(keypadTopic, async msg =>
        {
            await HandleKeypadMessageAsync(context, msg.Payload, msg.Topic);
        });
        _subscriptions.Add(sub);

        return ap;
    }

    public async Task<User> AddUserWithPinAsync(string name, string pin, string doorId, bool isActive = true)
    {
        var userId = $"usr_{Guid.NewGuid():N}";
        var user = new User
        {
            Id = userId,
            Name = name,
            Role = UserRole.Member,
            IsActive = isActive
        };
        await UserRepository.InsertAsync(user);

        var cred = new Credential
        {
            Id = $"cred_{Guid.NewGuid():N}",
            UserId = userId,
            Type = CredentialType.PIN,
            EncryptedValue = pin,
            HashedValue = AccessControl.Engine.Services.AccessPolicyEvaluator.ComputeSha256Hex(pin),
            PinLength = pin.Length,
            Label = $"{name} PIN"
        };
        await CredentialRepository.InsertAsync(cred);

        var policy = new AccessPolicy
        {
            Id = $"policy_{Guid.NewGuid():N}",
            Name = $"{name} 24/7 Access",
            ScheduleType = ScheduleType.Always,
            DaysOfWeek = 127,
            IsEnabled = true
        };
        await AccessPolicyRepository.InsertAsync(policy);

        await AccessPolicyRepository.AssignPolicyAsync(new AccessAssignment
        {
            Id = $"assign_{Guid.NewGuid():N}",
            AccessPointId = doorId,
            UserId = userId,
            PolicyId = policy.Id
        });

        return user;
    }

    public async Task RemoveUserAsync(string userId)
    {
        var user = await UserRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.IsActive = false;
            await UserRepository.UpdateAsync(user);
        }
        else
        {
            await UserRepository.DeleteAsync(userId);
        }
    }

    public async Task SimulateKeypadDisarmAsync(string keypadTopic, string pin)
    {
        var payload = JsonSerializer.Serialize(new
        {
            command = "disarm",
            code = pin,
            device_id = keypadTopic
        });

        await Broker.PublishAsync(keypadTopic, payload);
    }

    private async Task HandleKeypadMessageAsync(DoorContext context, string payload, string topic)
    {
        if (!context.KeypadProvider.TryParseKeypadEvent(topic, payload, out var keypadEvent))
        {
            return;
        }

        var result = await AccessPolicyEvaluator.EvaluateAsync(keypadEvent, context.Door);

        if (result.IsValid)
        {
            // Execute unlock
            await context.LockProvider.UnlockAsync();
            context.AutoLockStateMachine.OnLockStateChanged(LockState.Unlocked);

            // Record granted audit log
            var log = new AccessLog
            {
                Id = $"log_{Guid.NewGuid():N}",
                AccessPointId = context.Door.Id,
                UserId = result.User?.Id,
                UserName = result.User?.Name,
                CredentialType = CredentialType.PIN,
                EventType = AccessEventType.Unlocked,
                Method = AccessMethod.RingKeypad,
                Timestamp = DateTime.UtcNow,
                Details = $"Keypad unlock granted under policy {result.Policy?.Name}"
            };
            await AuditLogRepository.InsertAsync(log);

            // Publish HA event
            var haPayload = JsonSerializer.Serialize(new
            {
                event_type = "keypad_unlock",
                user = result.User?.Name,
                timestamp = DateTime.UtcNow
            });
            await Broker.PublishAsync($"accesscontrol/{context.Door.Id}/event/state", haPayload);
        }
        else
        {
            // Record denied audit log
            var log = new AccessLog
            {
                Id = $"log_{Guid.NewGuid():N}",
                AccessPointId = context.Door.Id,
                UserId = result.User?.Id,
                UserName = result.User?.Name ?? "Unknown",
                CredentialType = CredentialType.PIN,
                EventType = AccessEventType.Denied,
                Method = AccessMethod.RingKeypad,
                Timestamp = DateTime.UtcNow,
                Details = $"Access denied: {result.Reason}"
            };
            await AuditLogRepository.InsertAsync(log);
        }
    }

    public void SimulateLockStateChanged(string doorId, LockState lockState)
    {
        if (_doors.TryGetValue(doorId, out var context))
        {
            context.AutoLockStateMachine.OnLockStateChanged(lockState);
        }
    }

    public void SimulateDoorContactChanged(string doorId, DoorContactState contactState)
    {
        if (_doors.TryGetValue(doorId, out var context))
        {
            context.AutoLockStateMachine.OnDoorContactChanged(contactState);
        }
    }

    public async Task TriggerAutoLockExpiredAsync(string doorId)
    {
        if (_doors.TryGetValue(doorId, out var context))
        {
            await context.AutoLockStateMachine.TriggerCountdownExpiredAsync();
        }
    }

    public AutoLockStatus GetAutoLockStatus(string doorId)
    {
        if (_doors.TryGetValue(doorId, out var context))
        {
            return context.AutoLockStateMachine.Status;
        }
        return AutoLockStatus.Disabled;
    }

    public async Task SyncHardwareSlotsAsync(string doorId)
    {
        if (_doors.TryGetValue(doorId, out var context))
        {
            await HardwareSlotSyncWorker.ReconcileDoorSlotsAsync(context.Door, context.LockProvider);
        }
    }

    public Task<MqttPublishedMessage> WaitForPublishedMessageAsync(string topic, int timeoutMs = 500)
    {
        return Broker.WaitForPublishedMessageAsync(topic, timeoutMs);
    }

    public DoorContext GetDoorContext(string doorId)
    {
        return _doors[doorId];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }

        foreach (var door in _doors.Values)
        {
            door.AutoLockStateMachine.Dispose();
        }

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

    public sealed record DoorContext(
        AccessPoint Door,
        ILockProvider LockProvider,
        IKeypadProvider KeypadProvider,
        AutoLockStateMachine AutoLockStateMachine,
        string LockTopic,
        string KeypadTopic);
}
