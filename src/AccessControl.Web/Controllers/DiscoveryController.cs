using AccessControl.Engine.Services;
using Microsoft.AspNetCore.Mvc;

namespace AccessControl.Web.Controllers;

[ApiController]
[Route("api/discovery")]
public class DiscoveryController : ControllerBase
{
    private readonly IMqttDiscoveryService _discoveryService;

    public DiscoveryController(IMqttDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    [HttpGet("mqtt")]
    public IActionResult GetMqttDiscoveredTopics([FromQuery] string? type)
    {
        var topics = _discoveryService.GetDiscoveredTopics(type);
        return Ok(topics);
    }

    [HttpGet]
    public IActionResult GetAllDiscoveredTopics([FromQuery] string? type)
    {
        var topics = _discoveryService.GetDiscoveredTopics(type);
        return Ok(topics);
    }

    [HttpGet("sensors")]
    public IActionResult GetDiscoveredContactSensors()
    {
        var sensors = _discoveryService.GetDiscoveredContactSensors();
        return Ok(sensors);
    }

    [HttpGet("sniff")]
    public IActionResult SniffActivity([FromQuery] DateTimeOffset? since)
    {
        var referenceTime = since ?? DateTimeOffset.UtcNow.AddSeconds(-15);
        var result = _discoveryService.SniffActivity(referenceTime);
        return Ok(result);
    }
}
