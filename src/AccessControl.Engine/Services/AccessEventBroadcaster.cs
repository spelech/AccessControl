using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AccessControl.Core.Models;

namespace AccessControl.Engine.Services;

public interface IAccessEventBroadcaster
{
    void Broadcast(AccessLog log);
    IAsyncEnumerable<AccessLog> SubscribeAsync(CancellationToken ct);
}

public class AccessEventBroadcaster : IAccessEventBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<AccessLog>> _subscribers = new();

    public void Broadcast(AccessLog log)
    {
        foreach (var sub in _subscribers.Values)
        {
            sub.Writer.TryWrite(log);
        }
    }

    public async IAsyncEnumerable<AccessLog> SubscribeAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<AccessLog>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _subscribers[id] = channel;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                AccessLog item;
                try
                {
                    item = await channel.Reader.ReadAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                yield return item;
            }
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
        }
    }
}
