using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AccessControl.Core.Models;
using AccessControl.Core.Transports;
using AccessControl.Engine.Transports;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AccessControl.Tests.Unit;

public class ZWaveWebSocketTransportTests
{
    private class FakeWebSocketSession : IWebSocketSession
    {
        public WebSocketState State { get; set; } = WebSocketState.Open;
        public List<string> SentMessages { get; } = new();
        public Queue<string> IncomingMessages { get; } = new();

        public Task ConnectAsync(Uri uri, CancellationToken ct) => Task.CompletedTask;

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct)
        {
            var text = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
            SentMessages.Add(text);
            return Task.CompletedTask;
        }

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct)
        {
            if (IncomingMessages.TryDequeue(out var msg))
            {
                var bytes = Encoding.UTF8.GetBytes(msg);
                Array.Copy(bytes, 0, buffer.Array!, buffer.Offset, bytes.Length);
                return Task.FromResult(new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true));
            }

            // If empty, return a close result to terminate loop
            return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, "Done"));
        }

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken ct)
        {
            State = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    [Fact]
    public async Task ProcessIncomingMessage_VersionFrame_UpdatesVersionAndConnects()
    {
        var fakeSession = new FakeWebSocketSession();
        var transport = new ZWaveWebSocketTransport(
            "ws://localhost:3000",
            NullLogger<ZWaveWebSocketTransport>.Instance,
            () => fakeSession
        );

        await transport.StartAsync();

        var versionJson = JsonSerializer.Serialize(new
        {
            type = "version",
            driverVersion = "15.15.3",
            serverVersion = "3.2.1",
            homeId = 3756261977L
        });

        transport.ProcessIncomingMessage(versionJson);

        Assert.Equal(TransportStatus.Connected, transport.Status);
        Assert.NotNull(transport.VersionInfo);
        Assert.Equal("15.15.3", transport.VersionInfo.DriverVersion);
        Assert.Equal("3.2.1", transport.VersionInfo.ServerVersion);
        Assert.Equal(3756261977L, transport.VersionInfo.HomeId);

        // Verify start_listening was queued/sent
        Assert.Contains(fakeSession.SentMessages, m => m.Contains("start_listening"));
    }

    [Fact]
    public async Task ProcessIncomingMessage_DiscoveredNodes_ParsesLocksAndKeypads()
    {
        var fakeSession = new FakeWebSocketSession();
        var transport = new ZWaveWebSocketTransport(
            "ws://localhost:3000",
            NullLogger<ZWaveWebSocketTransport>.Instance,
            () => fakeSession
        );

        await transport.StartAsync();

        // Pre-create a pending request tcs for start_listening
        var resultJson = JsonSerializer.Serialize(new
        {
            type = "result",
            messageId = "test-msg-1",
            success = true,
            result = new
            {
                state = new
                {
                    nodes = new object[]
                    {
                        new { nodeId = 39, name = "Front Door Lock", deviceConfig = new { label = "Allegion BE469ZP" } },
                        new { nodeId = 40, name = "Laundry Room Keypad", deviceConfig = new { label = "Ring 4AK1SZ" } }
                    }
                }
            }
        });

        // Trigger async request to register messageId
        var sendTask = transport.SendRequestAsync(new ZWaveJsCommandRequest { MessageId = "test-msg-1", Command = "start_listening" });

        transport.ProcessIncomingMessage(resultJson);

        var nodes = transport.GetDiscoveredNodes();
        Assert.Equal(2, nodes.Count);
        Assert.Contains(nodes, n => n.NodeId == 39 && n.DeviceType == "lock");
        Assert.Contains(nodes, n => n.NodeId == 40 && n.DeviceType == "keypad");
    }

    [Fact]
    public void ProcessIncomingMessage_ValueUpdated_FiresLockStateChanged()
    {
        var transport = new ZWaveWebSocketTransport(
            "ws://localhost:3000",
            NullLogger<ZWaveWebSocketTransport>.Instance
        );

        LockStateUpdatedEventArgs? received = null;
        transport.OnLockStateChanged += e => received = e;

        var eventJson = JsonSerializer.Serialize(new
        {
            type = "event",
            @event = new
            {
                source = "node",
                @event = "value updated",
                nodeId = 39,
                args = new
                {
                    commandClass = 98,
                    property = "currentMode",
                    newValue = 255 // Locked
                }
            }
        });

        transport.ProcessIncomingMessage(eventJson);

        Assert.NotNull(received);
        Assert.Equal("39", received.DeviceTarget);
        Assert.Equal(LockState.Locked, received.State);
    }

    [Fact]
    public void ProcessIncomingMessage_KeypadNotification_FiresKeypadEntry()
    {
        var transport = new ZWaveWebSocketTransport(
            "ws://localhost:3000",
            NullLogger<ZWaveWebSocketTransport>.Instance
        );

        KeypadEntryEventArgs? received = null;
        transport.OnKeypadEntry += e => received = e;

        var eventJson = JsonSerializer.Serialize(new
        {
            type = "event",
            @event = new
            {
                source = "node",
                @event = "notification",
                nodeId = 40,
                args = new
                {
                    type = 6, // Access Control
                    @event = 6, // Keypad unlock
                    parameters = new
                    {
                        code = "1234"
                    }
                }
            }
        });

        transport.ProcessIncomingMessage(eventJson);

        Assert.NotNull(received);
        Assert.Equal("40", received.DeviceTarget);
        Assert.Equal("1234", received.Pin);
    }
}
