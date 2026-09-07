using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;
using AccessControl.Data.Repositories;
using AccessControl.Engine.Mqtt;
using AccessControl.Engine.Services;
using Microsoft.AspNetCore.Mvc;

namespace AccessControl.Web.Controllers;

[ApiController]
[Route("api/doors")]
public class AccessPointsController : ControllerBase
{
    private readonly IAccessPointRepository _doorRepo;
    private readonly IDoorOperationService _doorOps;
    private readonly IAuditLogRepository _auditRepo;
    private readonly IAccessEventBroadcaster _eventBroadcaster;
    private readonly IMqttClientService? _mqttClient;
    private readonly IHomeAssistantDiscoveryService? _haDiscovery;
    private readonly INotificationDispatcher? _notifier;
    private readonly ILogger<AccessPointsController> _logger;

    public AccessPointsController(
        IAccessPointRepository doorRepo,
        IDoorOperationService doorOps,
        IAuditLogRepository auditRepo,
        IAccessEventBroadcaster eventBroadcaster,
        ILogger<AccessPointsController> logger,
        IMqttClientService? mqttClient = null,
        IHomeAssistantDiscoveryService? haDiscovery = null,
        INotificationDispatcher? notifier = null)
    {
        _doorRepo = doorRepo;
        _doorOps = doorOps;
        _auditRepo = auditRepo;
        _eventBroadcaster = eventBroadcaster;
        _logger = logger;
        _mqttClient = mqttClient;
        _haDiscovery = haDiscovery;
        _notifier = notifier;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var doors = await _doorRepo.GetAllAsync(ct);
        var result = new List<object>();

        foreach (var door in doors)
        {
            var lockState = await _doorOps.GetDoorLockStateAsync(door.Id, ct);
            var contactState = await _doorOps.GetDoorContactStateAsync(door.Id, ct);
            var countdown = await _doorOps.GetRemainingAutoLockSecondsAsync(door.Id, ct);

            result.Add(new
            {
                id = door.Id,
                name = door.Name,
                lockProviderType = door.LockProviderType,
                lockConfigJson = door.LockConfigJson,
                keypadProviderType = door.KeypadProviderType,
                keypadConfigJson = door.KeypadConfigJson,
                doorSensorProviderType = door.DoorSensorProviderType,
                doorSensorConfigJson = door.DoorSensorConfigJson,
                autoLockEnabled = door.AutoLockEnabled,
                autoLockDaySeconds = door.AutoLockDaySeconds,
                autoLockNightSeconds = door.AutoLockNightSeconds,
                retryOnFailure = door.RetryOnFailure,
                lockState = lockState.ToString(),
                contactState = contactState.ToString(),
                remainingCountdownSeconds = countdown,
                createdAt = door.CreatedAt,
                updatedAt = door.UpdatedAt
            });
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var door = await _doorRepo.GetByIdAsync(id, ct);
        if (door == null)
        {
            return NotFound(new { error = $"Door '{id}' not found" });
        }

        var lockState = await _doorOps.GetDoorLockStateAsync(door.Id, ct);
        var contactState = await _doorOps.GetDoorContactStateAsync(door.Id, ct);
        var countdown = await _doorOps.GetRemainingAutoLockSecondsAsync(door.Id, ct);

        return Ok(new
        {
            id = door.Id,
            name = door.Name,
            lockProviderType = door.LockProviderType,
            lockConfigJson = door.LockConfigJson,
            keypadProviderType = door.KeypadProviderType,
            keypadConfigJson = door.KeypadConfigJson,
            doorSensorProviderType = door.DoorSensorProviderType,
            doorSensorConfigJson = door.DoorSensorConfigJson,
            autoLockEnabled = door.AutoLockEnabled,
            autoLockDaySeconds = door.AutoLockDaySeconds,
            autoLockNightSeconds = door.AutoLockNightSeconds,
            retryOnFailure = door.RetryOnFailure,
            lockState = lockState.ToString(),
            contactState = contactState.ToString(),
            remainingCountdownSeconds = countdown,
            createdAt = door.CreatedAt,
            updatedAt = door.UpdatedAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AccessPoint door, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(door.Name))
        {
            return BadRequest(new { error = "Door name is required" });
        }

        if (string.IsNullOrWhiteSpace(door.Id))
        {
            door.Id = Guid.NewGuid().ToString();
        }

        door.CreatedAt = DateTime.UtcNow;
        door.UpdatedAt = DateTime.UtcNow;

        await _doorRepo.InsertAsync(door, ct);
        _logger.LogInformation("Created access point '{Name}' ({Id})", door.Name, door.Id);

        if (_haDiscovery != null && _mqttClient != null && _mqttClient.IsConnected)
        {
            try
            {
                var msgs = _haDiscovery.BuildAllDiscoveryMessages(door);
                foreach (var m in msgs)
                {
                    await _mqttClient.PublishAsync(m.Topic, m.Payload, retain: true, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to publish HA discovery messages for new door '{DoorId}'", door.Id);
            }
        }

        return CreatedAtAction(nameof(GetById), new { id = door.Id }, door);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] AccessPoint door, CancellationToken ct)
    {
        var existing = await _doorRepo.GetByIdAsync(id, ct);
        if (existing == null)
        {
            return NotFound(new { error = $"Door '{id}' not found" });
        }

        door.Id = id;
        door.UpdatedAt = DateTime.UtcNow;
        door.CreatedAt = existing.CreatedAt;

        await _doorRepo.UpdateAsync(door, ct);
        _logger.LogInformation("Updated access point '{Name}' ({Id})", door.Name, id);

        if (_haDiscovery != null && _mqttClient != null && _mqttClient.IsConnected)
        {
            try
            {
                var msgs = _haDiscovery.BuildAllDiscoveryMessages(door);
                foreach (var m in msgs)
                {
                    await _mqttClient.PublishAsync(m.Topic, m.Payload, retain: true, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to republish HA discovery messages for door '{DoorId}'", id);
            }
        }

        return Ok(door);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var existing = await _doorRepo.GetByIdAsync(id, ct);
        if (existing == null)
        {
            return NotFound(new { error = $"Door '{id}' not found" });
        }

        await _doorRepo.DeleteAsync(id, ct);
        _logger.LogInformation("Deleted access point '{Name}' ({Id})", existing.Name, id);

        if (_haDiscovery != null && _mqttClient != null && _mqttClient.IsConnected)
        {
            try
            {
                var msgs = _haDiscovery.BuildAllDiscoveryMessages(existing);
                foreach (var m in msgs)
                {
                    await _mqttClient.PublishAsync(m.Topic, "", retain: true, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to unpublish HA discovery messages for door '{DoorId}'", id);
            }
        }

        return NoContent();
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock(string id, [FromQuery] int? durationMinutes, CancellationToken ct)
    {
        var door = await _doorRepo.GetByIdAsync(id, ct);
        if (door == null)
        {
            return NotFound(new { error = $"Door '{id}' not found" });
        }

        var success = await _doorOps.UnlockDoorAsync(id, durationMinutes, ct);
        if (!success)
        {
            return StatusCode(500, new { error = $"Failed to unlock door '{door.Name}'" });
        }

        var userName = HttpContext.User.Identity?.Name ?? "Web User";
        var log = new AccessLog
        {
            AccessPointId = id,
            UserName = userName,
            EventType = AccessEventType.Unlocked,
            Method = AccessMethod.Manual,
            Timestamp = DateTime.UtcNow,
            Details = durationMinutes.HasValue ? $"Remote unlock via API ({durationMinutes.Value}m auto-lock)" : "Remote unlock via API"
        };
        await _auditRepo.InsertAsync(log, ct);
        _eventBroadcaster.Broadcast(log);

        if (_notifier != null)
        {
            _ = _notifier.DispatchAccessEventAsync(log, ct);
        }

        return Ok(new { success = true, lockState = "Unlocked", doorId = id });
    }

    [HttpPost("{id}/lock")]
    public async Task<IActionResult> Lock(string id, CancellationToken ct)
    {
        var door = await _doorRepo.GetByIdAsync(id, ct);
        if (door == null)
        {
            return NotFound(new { error = $"Door '{id}' not found" });
        }

        var success = await _doorOps.LockDoorAsync(id, ct);
        if (!success)
        {
            return StatusCode(500, new { error = $"Failed to lock door '{door.Name}'" });
        }

        var userName = HttpContext.User.Identity?.Name ?? "Web User";
        var log = new AccessLog
        {
            AccessPointId = id,
            UserName = userName,
            EventType = AccessEventType.Locked,
            Method = AccessMethod.Manual,
            Timestamp = DateTime.UtcNow,
            Details = "Remote lock via API"
        };
        await _auditRepo.InsertAsync(log, ct);
        _eventBroadcaster.Broadcast(log);

        if (_notifier != null)
        {
            _ = _notifier.DispatchAccessEventAsync(log, ct);
        }

        return Ok(new { success = true, lockState = "Locked", doorId = id });
    }
}
