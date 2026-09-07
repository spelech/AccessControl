using System.Collections.Concurrent;
using AccessControl.Core.Transports;
using Microsoft.Extensions.Logging;

namespace AccessControl.Engine.Transports;

public class TransportRegistry : ITransportRegistry
{
    private readonly ConcurrentDictionary<string, ITransport> _transports = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<TransportRegistry> _logger;

    public TransportRegistry(ILogger<TransportRegistry> logger)
    {
        _logger = logger;
    }

    public void RegisterTransport(ITransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transports[transport.TransportId] = transport;
        _logger.LogInformation("Registered transport '{TransportId}' ({DisplayName})", transport.TransportId, transport.DisplayName);
    }

    public T? GetTransport<T>(string transportId) where T : class, ITransport
    {
        if (_transports.TryGetValue(transportId, out var transport) && transport is T typed)
        {
            return typed;
        }

        return null;
    }

    public IReadOnlyList<ITransport> GetAllTransports() => _transports.Values.ToList();

    public IEnumerable<T> GetTransports<T>() where T : class, ITransport =>
        _transports.Values.OfType<T>();

    public async Task StartAllAsync(CancellationToken ct = default)
    {
        foreach (var transport in _transports.Values)
        {
            try
            {
                _logger.LogInformation("Starting transport '{TransportId}'...", transport.TransportId);
                await transport.StartAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start transport '{TransportId}'", transport.TransportId);
            }
        }
    }

    public async Task StopAllAsync(CancellationToken ct = default)
    {
        foreach (var transport in _transports.Values)
        {
            try
            {
                _logger.LogInformation("Stopping transport '{TransportId}'...", transport.TransportId);
                await transport.StopAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop transport '{TransportId}'", transport.TransportId);
            }
        }
    }
}
