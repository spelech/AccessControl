using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using AccessControl.Mcp.Protocol;
using AccessControl.Mcp.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AccessControl.Mcp;

public interface IAccessControlMcpServer
{
    Task<McpRpcResponse> HandleRequestAsync(McpRpcRequest request, CancellationToken ct = default);
    Task HandleSseConnectionAsync(HttpContext context, CancellationToken ct = default);
    Task HandlePostMessageAsync(HttpContext context, string? sessionId, CancellationToken ct = default);
}

public class AccessControlMcpServer : IAccessControlMcpServer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccessControlMcpServer> _logger;
    private readonly ConcurrentDictionary<string, ChannelWriter<string>> _activeSessions = new();

    public AccessControlMcpServer(IServiceScopeFactory scopeFactory, ILogger<AccessControlMcpServer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<McpRpcResponse> HandleRequestAsync(McpRpcRequest request, CancellationToken ct = default)
    {
        if (request == null)
        {
            return McpRpcResponse.CreateError(null, -32600, "Invalid Request");
        }

        try
        {
            switch (request.Method)
            {
                case "initialize":
                    string? clientVersion = null;
                    if (request.Params.HasValue && request.Params.Value.TryGetProperty("protocolVersion", out var pv))
                    {
                        clientVersion = pv.GetString();
                    }

                    var negotiatedVersion = McpProtocolConstants.NegotiateVersion(clientVersion);

                    var initResult = new
                    {
                        protocolVersion = negotiatedVersion,
                        capabilities = new
                        {
                            tools = new { listChanged = false }
                        },
                        serverInfo = new
                        {
                            name = "accesscontrol",
                            version = "1.5.0"
                        }
                    };
                    return McpRpcResponse.Success(request.Id, initResult);

                case "notifications/initialized":
                    return McpRpcResponse.Success(request.Id, new { });

                case "ping":
                    return McpRpcResponse.Success(request.Id, new { });

                case "tools/list":
                {
                    using var scope = _scopeFactory.CreateScope();
                    var doorTools = scope.ServiceProvider.GetRequiredService<IDoorTools>();
                    var tools = doorTools.GetToolDefinitions();
                    return McpRpcResponse.Success(request.Id, new { tools });
                }

                case "tools/call":
                {
                    if (!request.Params.HasValue)
                    {
                        return McpRpcResponse.CreateError(request.Id, -32602, "Missing params object for tools/call");
                    }

                    var p = request.Params.Value;
                    if (!p.TryGetProperty("name", out var toolNameProp) || string.IsNullOrWhiteSpace(toolNameProp.GetString()))
                    {
                        return McpRpcResponse.CreateError(request.Id, -32602, "Missing required param: 'name'");
                    }

                    var toolName = toolNameProp.GetString()!;
                    JsonElement? toolArgs = null;
                    if (p.TryGetProperty("arguments", out var argsProp))
                    {
                        toolArgs = argsProp;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var doorTools = scope.ServiceProvider.GetRequiredService<IDoorTools>();
                    var callResult = await doorTools.ExecuteToolAsync(toolName, toolArgs, ct);
                    return McpRpcResponse.Success(request.Id, callResult);
                }

                default:
                    _logger.LogWarning("Unsupported MCP RPC method: {Method}", request.Method);
                    return McpRpcResponse.CreateError(request.Id, -32601, $"Method not found: '{request.Method}'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MCP request method '{Method}'", request.Method);
            return McpRpcResponse.CreateError(request.Id, -32603, $"Internal error: {ex.Message}");
        }
    }

    public async Task HandleSseConnectionAsync(HttpContext context, CancellationToken ct = default)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        var sessionId = Guid.NewGuid().ToString("N");
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _activeSessions[sessionId] = channel.Writer;

        _logger.LogInformation("New MCP SSE client connected. SessionId={SessionId}", sessionId);

        try
        {
            var endpointMessage = $"event: endpoint\ndata: /mcp/messages?sessionId={sessionId}\n\n";
            await context.Response.WriteAsync(endpointMessage, ct);
            await context.Response.Body.FlushAsync(ct);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            var reader = channel.Reader;

            while (!ct.IsCancellationRequested)
            {
                var readTask = reader.WaitToReadAsync(ct).AsTask();
                var timerTask = timer.WaitForNextTickAsync(ct).AsTask();

                var completed = await Task.WhenAny(readTask, timerTask);
                if (completed == readTask)
                {
                    while (reader.TryRead(out var msg))
                    {
                        await context.Response.WriteAsync(msg, ct);
                        await context.Response.Body.FlushAsync(ct);
                    }
                }
                else
                {
                    // Keepalive ping comment
                    await context.Response.WriteAsync(": ping\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on client disconnect
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP SSE connection terminated unexpectedly for SessionId={SessionId}", sessionId);
        }
        finally
        {
            _activeSessions.TryRemove(sessionId, out _);
            _logger.LogInformation("MCP SSE client disconnected. SessionId={SessionId}", sessionId);
        }
    }

    public async Task HandlePostMessageAsync(HttpContext context, string? sessionId, CancellationToken ct = default)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(body))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(McpRpcResponse.CreateError(null, -32700, "Parse error: empty request body"), ct);
            return;
        }

        McpRpcRequest? rpcRequest;
        try
        {
            rpcRequest = JsonSerializer.Deserialize<McpRpcRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(McpRpcResponse.CreateError(null, -32700, $"Parse error: {ex.Message}"), ct);
            return;
        }

        if (rpcRequest == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(McpRpcResponse.CreateError(null, -32600, "Invalid Request"), ct);
            return;
        }

        var rpcResponse = await HandleRequestAsync(rpcRequest, ct);
        var responseJson = JsonSerializer.Serialize(rpcResponse);

        // If session exists, broadcast on SSE channel as well
        if (!string.IsNullOrWhiteSpace(sessionId) && _activeSessions.TryGetValue(sessionId, out var sessionWriter))
        {
            var sseEvent = $"event: message\ndata: {responseJson}\n\n";
            sessionWriter.TryWrite(sseEvent);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync(responseJson, ct);
    }
}

public static class McpServerExtensions
{
    public static IServiceCollection AddAccessControlMcp(this IServiceCollection services)
    {
        services.AddScoped<IDoorTools, DoorTools>();
        services.AddSingleton<IAccessControlMcpServer, AccessControlMcpServer>();
        return services;
    }

    public static IEndpointRouteBuilder MapMcpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/mcp/sse", async (HttpContext context, IAccessControlMcpServer server, CancellationToken ct) =>
        {
            await server.HandleSseConnectionAsync(context, ct);
        });

        endpoints.MapPost("/mcp/messages", async (HttpContext context, IAccessControlMcpServer server, CancellationToken ct) =>
        {
            var sessionId = context.Request.Query["sessionId"].ToString();
            await server.HandlePostMessageAsync(context, sessionId, ct);
        });

        // Convenience alias endpoints
        endpoints.MapGet("/sse", async (HttpContext context, IAccessControlMcpServer server, CancellationToken ct) =>
        {
            await server.HandleSseConnectionAsync(context, ct);
        });

        return endpoints;
    }
}
