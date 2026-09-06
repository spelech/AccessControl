using System.Diagnostics;
using System.Net.Sockets;
using CodeMaster.Core.DTOs;
using CodeMaster.Core.Transports;
using CodeMaster.Engine.Services;
using CodeMaster.Engine.Transports;
using CodeMaster.Web.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CodeMaster.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly SystemSettingsService _settingsService;
    private readonly ITransportRegistry _transportRegistry;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        SystemSettingsService settingsService,
        ITransportRegistry transportRegistry,
        ILoggerFactory loggerFactory,
        ILogger<SettingsController> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _transportRegistry = transportRegistry ?? throw new ArgumentNullException(nameof(transportRegistry));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<ActionResult<SettingsResponseDto>> GetSettings(CancellationToken ct)
    {
        var settings = await _settingsService.GetSettingsAsync(ct);
        var transports = _transportRegistry.GetAllTransports().Select(t =>
        {
            string? details = null;
            if (t is ZWaveWebSocketTransport zwaveWs)
            {
                var count = zwaveWs.GetDiscoveredNodes().Count;
                details = count > 0 ? $"{count} Z-Wave nodes detected" : null;
            }

            return new TransportStatusDto(
                t.TransportId,
                t.DisplayName,
                t.Status.ToString(),
                t.IsConnected,
                details
            );
        }).ToList();

        return Ok(new SettingsResponseDto(settings, transports));
    }

    [HttpPut]
    public async Task<ActionResult> UpdateSettings([FromBody] SystemSettingsDto dto, CancellationToken ct)
    {
        if (dto == null)
        {
            return BadRequest(new { error = "Settings body is required." });
        }

        await _settingsService.SaveSettingsAsync(dto, ct);
        _logger.LogInformation("Updated system settings via REST API.");

        // Reconfigure active Z-Wave transport if URL or type changed
        var wsTransport = _transportRegistry.GetTransport<ZWaveWebSocketTransport>("zwave_ws");
        if (dto.ZWaveTransportType.Equals("WebSocket", StringComparison.OrdinalIgnoreCase))
        {
            if (wsTransport == null)
            {
                var newWs = new ZWaveWebSocketTransport(dto.ZWaveWebSocketUrl, _loggerFactory.CreateLogger<ZWaveWebSocketTransport>());
                _transportRegistry.RegisterTransport(newWs);
                _ = newWs.StartAsync(CancellationToken.None);
            }
        }

        return Ok(new { success = true, message = "Settings updated successfully." });
    }

    [HttpPost("test-connection")]
    public async Task<ActionResult<TestConnectionResponseDto>> TestConnection([FromBody] TestConnectionRequestDto req, CancellationToken ct)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.TransportType))
        {
            return BadRequest(new { error = "TransportType is required." });
        }

        var sw = Stopwatch.StartNew();

        if (req.TransportType.Equals("WebSocket", StringComparison.OrdinalIgnoreCase))
        {
            var url = string.IsNullOrWhiteSpace(req.EndpointUrl) ? "ws://10.0.0.10:8106" : req.EndpointUrl;
            try
            {
                var testWs = new ZWaveWebSocketTransport(url, _loggerFactory.CreateLogger<ZWaveWebSocketTransport>());
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                await testWs.StartAsync(linkedCts.Token);

                // Wait up to 3 seconds for version frame and start_listening reply
                var waited = 0;
                while (testWs.VersionInfo == null && waited < 3000)
                {
                    await Task.Delay(100, linkedCts.Token);
                    waited += 100;
                }

                sw.Stop();
                var nodes = testWs.GetDiscoveredNodes().Select(n =>
                    new ZWaveNodeSummaryDto(n.NodeId, n.Name, n.DeviceType, n.Model)).ToList();

                await testWs.StopAsync(CancellationToken.None);

                return Ok(new TestConnectionResponseDto(
                    Success: true,
                    LatencyMs: sw.ElapsedMilliseconds,
                    DriverVersion: testWs.VersionInfo?.DriverVersion ?? "Connected",
                    ServerVersion: testWs.VersionInfo?.ServerVersion,
                    NodeCount: nodes.Count,
                    DetectedNodes: nodes,
                    Message: $"Successfully connected to Z-Wave JS Server at {url}."
                ));
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogWarning(ex, "Test connection failed to Z-Wave WebSocket {Url}", url);
                return Ok(new TestConnectionResponseDto(
                    Success: false,
                    LatencyMs: sw.ElapsedMilliseconds,
                    Message: $"Connection failed: {ex.Message}"
                ));
            }
        }
        else if (req.TransportType.Equals("Mqtt", StringComparison.OrdinalIgnoreCase))
        {
            var host = string.IsNullOrWhiteSpace(req.MqttHost) ? "10.0.0.10" : req.MqttHost;
            var port = req.MqttPort ?? 8100;

            try
            {
                using var tcp = new TcpClient();
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await tcp.ConnectAsync(host, port, timeoutCts.Token);
                sw.Stop();

                return Ok(new TestConnectionResponseDto(
                    Success: true,
                    LatencyMs: sw.ElapsedMilliseconds,
                    Message: $"Successfully connected to MQTT broker at {host}:{port}."
                ));
            }
            catch (Exception ex)
            {
                sw.Stop();
                return Ok(new TestConnectionResponseDto(
                    Success: false,
                    LatencyMs: sw.ElapsedMilliseconds,
                    Message: $"Failed to connect to MQTT broker: {ex.Message}"
                ));
            }
        }

        return BadRequest(new { error = $"Unsupported transport type '{req.TransportType}'" });
    }
}
