using System.Text.Json;
using AccessControl.Core.Models;
using AccessControl.Data.Repositories;
using AccessControl.Engine.Services;
using Microsoft.AspNetCore.Mvc;

using System.Text.Json.Serialization;

namespace AccessControl.Web.Controllers;

[ApiController]
[Route("api/logs")]
public class AccessLogsController : ControllerBase
{
    private static readonly JsonSerializerOptions s_sseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IAuditLogRepository _auditRepo;
    private readonly IAccessEventBroadcaster _eventBroadcaster;
    private readonly ILogger<AccessLogsController> _logger;

    public AccessLogsController(
        IAuditLogRepository auditRepo,
        IAccessEventBroadcaster eventBroadcaster,
        ILogger<AccessLogsController> logger)
    {
        _auditRepo = auditRepo;
        _eventBroadcaster = eventBroadcaster;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? doorId,
        [FromQuery] string? userId,
        [FromQuery] string? method,
        [FromQuery] string? eventType,
        [FromQuery] int limit = 100,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        IReadOnlyList<AccessLog> rawLogs;

        if (!string.IsNullOrWhiteSpace(doorId))
        {
            rawLogs = await _auditRepo.GetRecentLogsAsync(doorId, limit * Math.Max(1, page), ct);
        }
        else
        {
            rawLogs = await _auditRepo.GetAllRecentLogsAsync(limit * Math.Max(1, page), ct);
        }

        var query = rawLogs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(l => string.Equals(l.UserId, userId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(method) && Enum.TryParse<AccessMethod>(method, true, out var parsedMethod))
        {
            query = query.Where(l => l.Method == parsedMethod);
        }

        if (!string.IsNullOrWhiteSpace(eventType) && Enum.TryParse<AccessEventType>(eventType, true, out var parsedEventType))
        {
            query = query.Where(l => l.EventType == parsedEventType);
        }

        var skip = (Math.Max(1, page) - 1) * limit;
        var paged = query.Skip(skip).Take(limit).ToList();

        return Ok(paged);
    }

    [HttpGet("stream")]
    public async Task StreamLiveEvents(CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        _logger.LogInformation("Client subscribed to SSE live access logs feed");

        try
        {
            // Initial heartbeat / comment
            await Response.WriteAsync(": connected\n\n", ct);
            await Response.Body.FlushAsync(ct);

            await foreach (var log in _eventBroadcaster.SubscribeAsync(ct))
            {
                var json = JsonSerializer.Serialize(log, s_sseJsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal client disconnect
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSE log stream disconnected unexpectedly");
        }
    }
}
