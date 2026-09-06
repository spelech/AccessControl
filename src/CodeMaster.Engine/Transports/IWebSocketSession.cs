using System.Net.WebSockets;

namespace CodeMaster.Engine.Transports;

public interface IWebSocketSession : IDisposable
{
    WebSocketState State { get; }
    Task ConnectAsync(Uri uri, CancellationToken ct);
    Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct);
    Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct);
    Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken ct);
}

public class DefaultClientWebSocketSession : IWebSocketSession
{
    private readonly ClientWebSocket _client = new();

    public WebSocketState State => _client.State;

    public Task ConnectAsync(Uri uri, CancellationToken ct) => _client.ConnectAsync(uri, ct);

    public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct) =>
        _client.SendAsync(buffer, messageType, endOfMessage, ct);

    public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct) =>
        _client.ReceiveAsync(buffer, ct);

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken ct) =>
        _client.CloseAsync(closeStatus, statusDescription, ct);

    public void Dispose() => _client.Dispose();
}
