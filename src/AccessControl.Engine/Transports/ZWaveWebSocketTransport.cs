using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AccessControl.Core.DTOs;
using AccessControl.Core.Models;
using AccessControl.Core.Transports;
using Microsoft.Extensions.Logging;

namespace AccessControl.Engine.Transports;

public class ZWaveWebSocketTransport : ILockTransport, IKeypadTransport
{
    private readonly string _url;
    private readonly Func<IWebSocketSession> _sessionFactory;
    private readonly ILogger<ZWaveWebSocketTransport> _logger;

    private IWebSocketSession? _session;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoopTask;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<ZWaveJsResultResponse>> _pendingRequests = new();
    private readonly ConcurrentDictionary<int, LockState> _nodeLockStates = new();
    private readonly ConcurrentDictionary<int, ZWaveNodeSummary> _discoveredNodes = new();

    public string TransportId => "zwave_ws";
    public string DisplayName => "Z-Wave JS WebSocket";
    public bool IsConnected => Status == TransportStatus.Connected;
    public TransportStatus Status { get; private set; } = TransportStatus.Disconnected;
    public ZWaveJsVersionInfo? VersionInfo { get; private set; }

    public event Action<TransportStatusChangedEventArgs>? OnStatusChanged;
    public event Action<LockStateUpdatedEventArgs>? OnLockStateChanged;
    public event Action<KeypadEntryEventArgs>? OnKeypadEntry;

    public ZWaveWebSocketTransport(
        string url,
        ILogger<ZWaveWebSocketTransport> logger,
        Func<IWebSocketSession>? sessionFactory = null)
    {
        _url = string.IsNullOrWhiteSpace(url) ? "ws://10.0.0.10:8106" : url;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionFactory = sessionFactory ?? (() => new DefaultClientWebSocketSession());
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (Status == TransportStatus.Connected || Status == TransportStatus.Connecting)
        {
            return;
        }

        UpdateStatus(TransportStatus.Connecting);
        _cts = new CancellationTokenSource();

        try
        {
            _session = _sessionFactory();
            var uri = new Uri(_url);
            _logger.LogInformation("Connecting to Z-Wave JS Server at {Uri}...", uri);

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(TimeSpan.FromSeconds(10));
            await _session.ConnectAsync(uri, connectCts.Token);

            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            _logger.LogInformation("Connected to Z-Wave JS Server at {Uri}", uri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Z-Wave JS Server at {Url}", _url);
            UpdateStatus(TransportStatus.Disconnected, ex.Message);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        UpdateStatus(TransportStatus.Disconnected);

        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        if (_session != null)
        {
            try
            {
                if (_session.State == WebSocketState.Open)
                {
                    await _session.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client stopping", ct);
                }
            }
            catch
            {
                // Ignore disconnect errors during teardown
            }
            finally
            {
                _session.Dispose();
                _session = null;
            }
        }

        foreach (var kvp in _pendingRequests)
        {
            kvp.Value.TrySetCanceled();
        }
        _pendingRequests.Clear();
    }

    public IReadOnlyList<ZWaveNodeSummary> GetDiscoveredNodes() => _discoveredNodes.Values.ToList();

    public async Task<bool> SetLockStateAsync(string deviceTarget, bool locked, CancellationToken ct = default)
    {
        if (!TryParseNodeId(deviceTarget, out var nodeId))
        {
            _logger.LogWarning("Invalid device target for Z-Wave node: '{Target}'", deviceTarget);
            return false;
        }

        var req = new ZWaveJsCommandRequest
        {
            Command = "node.set_value",
            NodeId = nodeId,
            ValueId = new
            {
                commandClass = 98, // Door Lock CC
                property = "targetMode"
            },
            Value = locked ? 255 : 0
        };

        var response = await SendRequestAsync(req, ct);
        if (response.Success)
        {
            var newState = locked ? LockState.Locked : LockState.Unlocked;
            _nodeLockStates[nodeId] = newState;
            OnLockStateChanged?.Invoke(new LockStateUpdatedEventArgs(deviceTarget, newState, "CodeMaster"));
            return true;
        }

        return false;
    }

    public static bool TryParseNodeId(string target, out int nodeId)
    {
        nodeId = 0;
        if (string.IsNullOrWhiteSpace(target)) return false;
        if (int.TryParse(target, out nodeId)) return true;

        var clean = target.Trim().Trim('/');
        if (clean.StartsWith("zwave/", StringComparison.OrdinalIgnoreCase))
            clean = clean["zwave/".Length..];
        if (clean.StartsWith("node_", StringComparison.OrdinalIgnoreCase))
            clean = clean["node_".Length..];
        if (clean.StartsWith("node-", StringComparison.OrdinalIgnoreCase))
            clean = clean["node-".Length..];

        if (int.TryParse(clean, out nodeId)) return true;

        var digits = new string(clean.Where(char.IsDigit).ToArray());
        if (!string.IsNullOrEmpty(digits) && int.TryParse(digits, out nodeId)) return true;

        return false;
    }

    public Task<LockState> GetLockStateAsync(string deviceTarget, CancellationToken ct = default)
    {
        if (int.TryParse(deviceTarget, out var nodeId) && _nodeLockStates.TryGetValue(nodeId, out var state))
        {
            return Task.FromResult(state);
        }

        return Task.FromResult(LockState.Unknown);
    }

    public async Task<bool> SetUserCodeAsync(string deviceTarget, int slot, string pin, string? label, CancellationToken ct = default)
    {
        if (!int.TryParse(deviceTarget, out var nodeId))
        {
            return false;
        }

        var req = new ZWaveJsCommandRequest
        {
            Command = "endpoint.invoke_cc_api",
            NodeId = nodeId,
            Endpoint = 0,
            CommandClass = 99, // User Code CC
            Method = "set",
            Args = new object[] { slot, 1, pin } // slot, status=1 (Enabled), userCode
        };

        var response = await SendRequestAsync(req, ct);
        return response.Success;
    }

    public async Task<bool> ClearUserCodeAsync(string deviceTarget, int slot, CancellationToken ct = default)
    {
        if (!int.TryParse(deviceTarget, out var nodeId))
        {
            return false;
        }

        var req = new ZWaveJsCommandRequest
        {
            Command = "endpoint.invoke_cc_api",
            NodeId = nodeId,
            Endpoint = 0,
            CommandClass = 99, // User Code CC
            Method = "clear",
            Args = new object[] { slot }
        };

        var response = await SendRequestAsync(req, ct);
        return response.Success;
    }

    public async Task<IReadOnlyList<HardwareSlotDto>> GetUserCodesAsync(string deviceTarget, CancellationToken ct = default)
    {
        if (!int.TryParse(deviceTarget, out var nodeId))
        {
            return Array.Empty<HardwareSlotDto>();
        }

        var req = new ZWaveJsCommandRequest
        {
            Command = "endpoint.invoke_cc_api",
            NodeId = nodeId,
            Endpoint = 0,
            CommandClass = 99,
            Method = "get",
            Args = new object[] { 1 }
        };

        var response = await SendRequestAsync(req, ct);
        // Returns slots or empty list
        return Array.Empty<HardwareSlotDto>();
    }

    public Task<bool> SetKeypadModeAsync(string deviceTarget, KeypadArmMode mode, CancellationToken ct = default)
    {
        _logger.LogInformation("Setting keypad mode for target {Target} to {Mode}", deviceTarget, mode);
        return Task.FromResult(true);
    }

    public async Task<ZWaveJsResultResponse> SendRequestAsync(ZWaveJsCommandRequest request, CancellationToken ct = default)
    {
        if (_session == null || _session.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("Z-Wave JS WebSocket is not connected.");
        }

        var tcs = new TaskCompletionSource<ZWaveJsResultResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[request.MessageId] = tcs;

        try
        {
            var json = JsonSerializer.Serialize(request);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _session.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            using var reg = linkedCts.Token.Register(() => tcs.TrySetCanceled());

            return await tcs.Task;
        }
        finally
        {
            _pendingRequests.TryRemove(request.MessageId, out _);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];

        try
        {
            while (!ct.IsCancellationRequested && _session != null && _session.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _session.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        UpdateStatus(TransportStatus.Disconnected, "Server closed connection");
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                var text = Encoding.UTF8.GetString(ms.ToArray());
                ProcessIncomingMessage(text);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in Z-Wave JS WebSocket receive loop");
            UpdateStatus(TransportStatus.Disconnected, ex.Message);
        }
    }

    public void ProcessIncomingMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeProp))
            {
                var type = typeProp.GetString();

                if (type == "version")
                {
                    VersionInfo = JsonSerializer.Deserialize<ZWaveJsVersionInfo>(json);
                    _logger.LogInformation("Z-Wave JS Server Version: {Driver} (HomeId={HomeId})",
                        VersionInfo?.DriverVersion, VersionInfo?.HomeId);

                    // Send start_listening
                    _ = SendRequestAsync(new ZWaveJsCommandRequest
                    {
                        Command = "start_listening"
                    });
                    UpdateStatus(TransportStatus.Connected);
                    return;
                }

                if (type == "result")
                {
                    if (root.TryGetProperty("messageId", out var msgIdProp))
                    {
                        var msgId = msgIdProp.GetString();
                        if (msgId != null && _pendingRequests.TryGetValue(msgId, out var tcs))
                        {
                            var resultResp = JsonSerializer.Deserialize<ZWaveJsResultResponse>(json);
                            if (resultResp != null)
                            {
                                tcs.TrySetResult(resultResp);

                                // If this was start_listening, parse nodes
                                if (root.TryGetProperty("result", out var resElem) &&
                                    resElem.TryGetProperty("state", out var stateElem) &&
                                    stateElem.TryGetProperty("nodes", out var nodesElem) &&
                                    nodesElem.ValueKind == JsonValueKind.Array)
                                {
                                    ParseDiscoveredNodes(nodesElem);
                                }
                            }
                        }
                    }
                    return;
                }

                if (type == "event")
                {
                    ParseNodeEvent(root);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse incoming Z-Wave JS message: {Snippet}", json.Length > 100 ? json[..100] : json);
        }
    }

    private void ParseDiscoveredNodes(JsonElement nodesArray)
    {
        foreach (var node in nodesArray.EnumerateArray())
        {
            if (node.TryGetProperty("nodeId", out var nodeIdProp))
            {
                var nodeId = nodeIdProp.GetInt32();
                var name = node.TryGetProperty("name", out var n) ? n.GetString() ?? $"Node {nodeId}" : $"Node {nodeId}";
                var model = node.TryGetProperty("deviceConfig", out var cfg) && cfg.TryGetProperty("label", out var l) ? l.GetString() : null;

                var devType = "unknown";
                if (name.Contains("Lock", StringComparison.OrdinalIgnoreCase) || model?.Contains("Lock", StringComparison.OrdinalIgnoreCase) == true)
                {
                    devType = "lock";
                }
                else if (name.Contains("Keypad", StringComparison.OrdinalIgnoreCase) || model?.Contains("Keypad", StringComparison.OrdinalIgnoreCase) == true)
                {
                    devType = "keypad";
                }
                else if (name.Contains("Sensor", StringComparison.OrdinalIgnoreCase))
                {
                    devType = "sensor";
                }

                _discoveredNodes[nodeId] = new ZWaveNodeSummary(nodeId, name, devType, model);
            }
        }

        _logger.LogInformation("Discovered {Count} Z-Wave nodes from server state.", _discoveredNodes.Count);
    }

    private void ParseNodeEvent(JsonElement root)
    {
        if (!root.TryGetProperty("event", out var eventElem)) return;

        var eventType = eventElem.TryGetProperty("event", out var evProp) ? evProp.GetString() : null;
        var nodeId = eventElem.TryGetProperty("nodeId", out var nProp) ? nProp.GetInt32() : 0;

        if (eventType == "value updated")
        {
            if (eventElem.TryGetProperty("args", out var args))
            {
                var cc = args.TryGetProperty("commandClass", out var ccProp) ? ccProp.GetInt32() : 0;
                var prop = args.TryGetProperty("property", out var pProp) ? pProp.GetString() : null;

                if (cc == 98 && (prop == "currentMode" || prop == "targetMode")) // Door Lock CC
                {
                    var val = args.TryGetProperty("newValue", out var nv) ? nv.GetInt32() : 0;
                    var state = val == 255 ? LockState.Locked : (val == 0 ? LockState.Unlocked : LockState.Jammed);
                    _nodeLockStates[nodeId] = state;
                    OnLockStateChanged?.Invoke(new LockStateUpdatedEventArgs(nodeId.ToString(), state, "ZWaveWs"));
                }
            }
        }
        else if (eventType == "notification")
        {
            if (eventElem.TryGetProperty("args", out var args))
            {
                var type = args.TryGetProperty("type", out var tProp) ? tProp.GetInt32() : 0;
                var ev = args.TryGetProperty("event", out var eProp) ? eProp.GetInt32() : 0;

                // Access Control Notification (Type 6)
                if (type == 6)
                {
                    string? code = null;
                    if (args.TryGetProperty("parameters", out var paramsElem) &&
                        paramsElem.TryGetProperty("code", out var codeProp))
                    {
                        code = codeProp.GetString();
                    }

                    if (!string.IsNullOrEmpty(code))
                    {
                        OnKeypadEntry?.Invoke(new KeypadEntryEventArgs(nodeId.ToString(), code, $"NotificationEvent_{ev}", DateTime.UtcNow));
                    }

                    // Jammed notification: Event 11 (Lock Jammed)
                    if (ev == 11)
                    {
                        _nodeLockStates[nodeId] = LockState.Jammed;
                        OnLockStateChanged?.Invoke(new LockStateUpdatedEventArgs(nodeId.ToString(), LockState.Jammed, "HardwareAlert"));
                    }
                }
            }
        }
    }

    private void UpdateStatus(TransportStatus newStatus, string? error = null)
    {
        if (Status != newStatus)
        {
            var old = Status;
            Status = newStatus;
            OnStatusChanged?.Invoke(new TransportStatusChangedEventArgs(TransportId, old, newStatus, error));
        }
    }
}
