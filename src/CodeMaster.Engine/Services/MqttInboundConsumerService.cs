using System.Text.Json;
using CodeMaster.Core.DTOs;
using CodeMaster.Core.Interfaces;
using CodeMaster.Core.Models;
using CodeMaster.Data.Repositories;
using CodeMaster.Engine.Channels;
using CodeMaster.Engine.Mqtt;
using CodeMaster.Engine.Providers.Keypads;
using CodeMaster.Engine.Providers.Locks;
using CodeMaster.Engine.Providers.Sensors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeMaster.Engine.Services;

/// <summary>
/// Hosted background service that consumes inbound MQTT messages from the bounded channel,
/// routes keypad PIN entries to access evaluation, synchronizes lock and door sensor states,
/// records immutable audit logs, and broadcasts live events.
/// </summary>
public class MqttInboundConsumerService : BackgroundService
{
    private readonly IMqttInboundChannel _inboundChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMqttDiscoveryService _discoveryService;
    private readonly ILogger<MqttInboundConsumerService> _logger;
    private readonly HashSet<string> _discoveredDoorsPublished = new(StringComparer.OrdinalIgnoreCase);

    public MqttInboundConsumerService(
        IMqttInboundChannel inboundChannel,
        IServiceScopeFactory scopeFactory,
        IMqttDiscoveryService discoveryService,
        ILogger<MqttInboundConsumerService> logger)
    {
        _inboundChannel = inboundChannel ?? throw new ArgumentNullException(nameof(inboundChannel));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MqttInboundConsumerService starting message consumption loop...");

        // Initial delay to allow MQTT client and DB seeder to complete startup
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var message in _inboundChannel.Reader.ReadAllAsync(stoppingToken))
                {
                    if (string.IsNullOrWhiteSpace(message.Topic))
                    {
                        continue;
                    }

                    // 1. Dynamic Topic Discovery registration
                    _discoveryService.RecordTopic(message.Topic, message.Payload);

                    // 2. Process message within dedicated scope
                    await ProcessMessageAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in MQTT inbound consumption pipeline. Continuing loop...");
                await Task.Delay(500, stoppingToken);
            }
        }

        _logger.LogInformation("MqttInboundConsumerService stopped.");
    }

    public async Task ProcessMessageAsync(MqttInboundMessage message, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var doorRepo = scope.ServiceProvider.GetRequiredService<IAccessPointRepository>();
        var doorOps = scope.ServiceProvider.GetRequiredService<IDoorOperationService>();
        var evaluator = scope.ServiceProvider.GetRequiredService<IAccessPolicyEvaluator>();
        var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
        var broadcaster = scope.ServiceProvider.GetRequiredService<IAccessEventBroadcaster>();
        var mqttClient = scope.ServiceProvider.GetService<IMqttClientService>();
        var haDiscovery = scope.ServiceProvider.GetService<IHomeAssistantDiscoveryService>();
        var notifier = scope.ServiceProvider.GetService<INotificationDispatcher>();

        var doors = await doorRepo.GetAllAsync(ct);
        if (doors.Count == 0)
        {
            return;
        }

        // Auto-publish HA discovery payloads once per door if MQTT client is connected
        if (haDiscovery != null && mqttClient != null && mqttClient.IsConnected)
        {
            foreach (var door in doors)
            {
                if (_discoveredDoorsPublished.Add(door.Id))
                {
                    try
                    {
                        var msgs = haDiscovery.BuildAllDiscoveryMessages(door);
                        foreach (var m in msgs)
                        {
                            await mqttClient.PublishAsync(m.Topic, m.Payload, retain: true, ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not publish HA discovery for door '{DoorId}'", door.Id);
                    }
                }
            }
        }

        foreach (var door in doors)
        {
            // A. Check Keypad Entry
            var keypadHandled = await TryHandleKeypadEventAsync(
                door,
                message,
                evaluator,
                doorOps,
                auditRepo,
                broadcaster,
                mqttClient,
                notifier,
                ct);

            if (keypadHandled)
            {
                continue;
            }

            // B. Check Lock State Update
            TryHandleLockTelemetry(door, message, doorOps, mqttClient);

            // C. Check Contact Sensor Update
            TryHandleContactTelemetry(door, message, doorOps);
        }
    }

    private async Task<bool> TryHandleKeypadEventAsync(
        AccessPoint door,
        MqttInboundMessage message,
        IAccessPolicyEvaluator evaluator,
        IDoorOperationService doorOps,
        IAuditLogRepository auditRepo,
        IAccessEventBroadcaster broadcaster,
        IMqttClientService? mqttClient,
        INotificationDispatcher? notifier,
        CancellationToken ct)
    {
        var kpType = door.KeypadProviderType ?? string.Empty;
        if (string.IsNullOrWhiteSpace(kpType) || kpType.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Determine configured keypad topic
        string keypadTopic = "ring";
        if (!string.IsNullOrWhiteSpace(door.KeypadConfigJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(door.KeypadConfigJson);
                if (doc.RootElement.TryGetProperty("topic", out var tProp) ||
                    doc.RootElement.TryGetProperty("keypadTopic", out tProp))
                {
                    keypadTopic = tProp.GetString() ?? keypadTopic;
                }
            }
            catch
            {
                // Fallback
            }
        }

        IKeypadProvider keypadProvider;
        if (kpType.Contains("Ring", StringComparison.OrdinalIgnoreCase))
        {
            keypadProvider = new RingMqttKeypadProvider(keypadTopic, mqttClient);
        }
        else
        {
            keypadProvider = new BuiltInLockKeypadProvider(keypadTopic, mqttClient);
        }

        if (!keypadProvider.TryParseKeypadEvent(message, out var kpEvent) || kpEvent == null)
        {
            return false;
        }

        _logger.LogInformation("Parsed keypad event: Action={Action} DeviceId={DeviceId} on Door '{DoorName}'",
            kpEvent.Action, kpEvent.DeviceId, door.Name);

        if (kpEvent.Action is KeypadAction.Unlock or KeypadAction.Disarm)
        {
            var result = await evaluator.EvaluateAsync(kpEvent, door, null, ct);
            if (result.IsValid)
            {
                await doorOps.UnlockDoorAsync(door.Id, null, ct);

                var log = new AccessLog
                {
                    AccessPointId = door.Id,
                    UserId = result.User?.Id,
                    UserName = result.User?.Name ?? "Authorized User",
                    CredentialType = CredentialType.PIN,
                    EventType = AccessEventType.Unlocked,
                    Method = kpType.Contains("Ring", StringComparison.OrdinalIgnoreCase)
                        ? AccessMethod.RingKeypad
                        : AccessMethod.BuiltInKeypad,
                    Timestamp = DateTime.UtcNow,
                    Details = $"Keypad unlock granted under policy '{result.Policy?.Name ?? "Default"}'"
                };

                await auditRepo.InsertAsync(log, ct);
                broadcaster.Broadcast(log);

                if (notifier != null)
                {
                    _ = notifier.DispatchAccessEventAsync(log, ct);
                }

                if (mqttClient != null && mqttClient.IsConnected)
                {
                    var haPayload = JsonSerializer.Serialize(new
                    {
                        event_type = "keypad_unlock",
                        user = result.User?.Name,
                        door = door.Name,
                        timestamp = DateTime.UtcNow
                    });
                    _ = mqttClient.PublishAsync($"codemaster/{door.Id}/event/state", haPayload, retain: false, ct);
                }
            }
            else
            {
                var log = new AccessLog
                {
                    AccessPointId = door.Id,
                    UserId = result.User?.Id,
                    UserName = result.User?.Name ?? "Unknown Subject",
                    CredentialType = CredentialType.PIN,
                    EventType = AccessEventType.Denied,
                    Method = kpType.Contains("Ring", StringComparison.OrdinalIgnoreCase)
                        ? AccessMethod.RingKeypad
                        : AccessMethod.BuiltInKeypad,
                    Timestamp = DateTime.UtcNow,
                    Details = $"Keypad unlock denied: {result.Reason}"
                };

                await auditRepo.InsertAsync(log, ct);
                broadcaster.Broadcast(log);

                if (notifier != null)
                {
                    _ = notifier.DispatchAccessEventAsync(log, ct);
                }
            }

            return true;
        }

        if (kpEvent.Action is KeypadAction.Lock or KeypadAction.ArmAway or KeypadAction.ArmStay)
        {
            await doorOps.LockDoorAsync(door.Id, ct);

            var log = new AccessLog
            {
                AccessPointId = door.Id,
                UserName = "Keypad Lock Command",
                EventType = AccessEventType.Locked,
                Method = kpType.Contains("Ring", StringComparison.OrdinalIgnoreCase)
                    ? AccessMethod.RingKeypad
                    : AccessMethod.BuiltInKeypad,
                Timestamp = DateTime.UtcNow,
                Details = $"Keypad lock command '{kpEvent.Action}' executed"
            };

            await auditRepo.InsertAsync(log, ct);
            broadcaster.Broadcast(log);

            return true;
        }

        return false;
    }

    private static void TryHandleLockTelemetry(
        AccessPoint door,
        MqttInboundMessage message,
        IDoorOperationService doorOps,
        IMqttClientService? mqttClient)
    {
        // 1. Check direct standard telemetry topic: codemaster/{doorId}/lock/state
        if (message.Topic.Equals($"codemaster/{door.Id}/lock/state", StringComparison.OrdinalIgnoreCase))
        {
            var p = message.Payload.Trim().Trim('"').ToLowerInvariant();
            if (p is "locked" or "lock" or "255")
            {
                doorOps.UpdateDoorStates(door.Id, lockState: LockState.Locked);
                return;
            }
            if (p is "unlocked" or "unlock" or "0")
            {
                doorOps.UpdateDoorStates(door.Id, lockState: LockState.Unlocked);
                return;
            }
            if (p is "jammed")
            {
                doorOps.UpdateDoorStates(door.Id, lockState: LockState.Jammed);
                return;
            }
        }

        // 2. Check provider-specific topics (Z-Wave JS or Generic MQTT)
        var lockType = door.LockProviderType ?? string.Empty;
        string? configuredTopic = null;

        if (!string.IsNullOrWhiteSpace(door.LockConfigJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(door.LockConfigJson);
                if (doc.RootElement.TryGetProperty("topic", out var tProp) ||
                    doc.RootElement.TryGetProperty("lockTopic", out tProp) ||
                    doc.RootElement.TryGetProperty("nodeId", out tProp))
                {
                    configuredTopic = tProp.GetString();
                }
            }
            catch
            {
                // Ignore parse errors
            }
        }

        if (lockType.Contains("ZWave", StringComparison.OrdinalIgnoreCase))
        {
            var zwaveProvider = new ZWaveJsMqttLockProvider(configuredTopic ?? door.Id, 0, mqttClient);
            if (zwaveProvider.TryUpdateFromMessage(message))
            {
                doorOps.UpdateDoorStates(door.Id, lockState: zwaveProvider.GetStateAsync().GetAwaiter().GetResult());
            }
        }
        else
        {
            var cmdTopic = configuredTopic ?? $"codemaster/{door.Id}/lock/set";
            var stateTopic = configuredTopic ?? $"codemaster/{door.Id}/lock/state";
            var genericProvider = new GenericMqttLockProvider(cmdTopic, stateTopic, "LOCK", "UNLOCK", mqttClient);
            if (genericProvider.TryUpdateFromMessage(message))
            {
                doorOps.UpdateDoorStates(door.Id, lockState: genericProvider.GetStateAsync().GetAwaiter().GetResult());
            }
        }
    }

    private static void TryHandleContactTelemetry(
        AccessPoint door,
        MqttInboundMessage message,
        IDoorOperationService doorOps)
    {
        // 1. Direct standard contact topic: codemaster/{doorId}/sensor/state
        if (message.Topic.Equals($"codemaster/{door.Id}/sensor/state", StringComparison.OrdinalIgnoreCase))
        {
            var p = message.Payload.Trim().Trim('"').ToUpperInvariant();
            if (p is "ON" or "OPEN" or "TRUE" or "1")
            {
                doorOps.UpdateDoorStates(door.Id, contactState: DoorContactState.Open);
                return;
            }
            if (p is "OFF" or "CLOSED" or "FALSE" or "0")
            {
                doorOps.UpdateDoorStates(door.Id, contactState: DoorContactState.Closed);
                return;
            }
        }

        // 2. Provider-specific sensor topic
        if (string.IsNullOrWhiteSpace(door.DoorSensorProviderType) ||
            door.DoorSensorProviderType.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? sensorTopic = null;
        if (!string.IsNullOrWhiteSpace(door.DoorSensorConfigJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(door.DoorSensorConfigJson);
                if (doc.RootElement.TryGetProperty("topic", out var tProp) ||
                    doc.RootElement.TryGetProperty("sensorTopic", out tProp))
                {
                    sensorTopic = tProp.GetString();
                }
            }
            catch
            {
                // Ignore parse errors
            }
        }

        var sensorProvider = new MqttContactSensorProvider(new MqttContactSensorOptions
        {
            Topic = sensorTopic,
            OpenPayload = "ON",
            ClosedPayload = "OFF"
        });

        if (sensorProvider.TryParseContactEvent(message, out var contactState) && contactState.HasValue)
        {
            doorOps.UpdateDoorStates(door.Id, contactState: contactState.Value);
        }
    }
}
