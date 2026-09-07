using System.Collections.Concurrent;
using AccessControl.Engine.Mqtt;

namespace AccessControl.Tests.Harness;

public record MqttPublishedMessage(string Topic, string Payload, bool Retain, DateTimeOffset Timestamp);

/// <summary>
/// Synthetic in-memory broker that simulates MQTT publish/subscribe,
/// allows registering topic listeners, and records all published messages for assertions.
/// Also implements IMqttClientService for direct injection into engine components.
/// </summary>
public class MockMqttBroker : IMqttClientService
{
    private readonly ConcurrentQueue<MqttPublishedMessage> _messages = new();
    private readonly List<(string Filter, Func<MqttPublishedMessage, Task> Handler)> _subscriptions = new();
    private readonly List<WaitingSubscription> _waiters = new();
    private readonly object _syncLock = new();

    public bool IsConnected => true;

    public IReadOnlyList<MqttPublishedMessage> PublishedMessages
    {
        get
        {
            lock (_syncLock)
            {
                return _messages.ToList();
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void ClearMessages()
    {
        lock (_syncLock)
        {
            _messages.Clear();
        }
    }

    public async Task PublishAsync(string topic, string payload, bool retain = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(payload);

        var message = new MqttPublishedMessage(topic, payload, retain, DateTimeOffset.UtcNow);

        List<Func<MqttPublishedMessage, Task>> matchedHandlers;
        List<TaskCompletionSource<MqttPublishedMessage>> matchedWaiters = new();

        lock (_syncLock)
        {
            _messages.Enqueue(message);

            matchedHandlers = _subscriptions
                .Where(s => IsTopicMatch(s.Filter, topic))
                .Select(s => s.Handler)
                .ToList();

            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                var waiter = _waiters[i];
                if (waiter.Predicate(message))
                {
                    matchedWaiters.Add(waiter.Tcs);
                    _waiters.RemoveAt(i);
                }
            }
        }

        foreach (var tcs in matchedWaiters)
        {
            tcs.TrySetResult(message);
        }

        foreach (var handler in matchedHandlers)
        {
            await handler(message);
        }
    }

    public IDisposable Subscribe(string topicFilter, Func<MqttPublishedMessage, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicFilter);
        ArgumentNullException.ThrowIfNull(handler);

        lock (_syncLock)
        {
            var tuple = (topicFilter, handler);
            _subscriptions.Add(tuple);
            return new SubscriptionToken(() =>
            {
                lock (_syncLock)
                {
                    _subscriptions.Remove(tuple);
                }
            });
        }
    }

    public IDisposable Subscribe(string topicFilter, Action<MqttPublishedMessage> handler)
    {
        return Subscribe(topicFilter, msg =>
        {
            handler(msg);
            return Task.CompletedTask;
        });
    }

    public Task<MqttPublishedMessage> WaitForPublishedMessageAsync(string topic, int timeoutMs = 1000)
    {
        return WaitForPublishedMessageAsync(
            msg => msg.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase) ||
                   msg.Topic.StartsWith(topic, StringComparison.OrdinalIgnoreCase) ||
                   IsTopicMatch(topic, msg.Topic),
            timeoutMs);
    }

    public async Task<MqttPublishedMessage> WaitForPublishedMessageAsync(Func<MqttPublishedMessage, bool> predicate, int timeoutMs = 1000)
    {
        TaskCompletionSource<MqttPublishedMessage> tcs;
        using var cts = new CancellationTokenSource(timeoutMs);

        lock (_syncLock)
        {
            var existing = _messages.FirstOrDefault(predicate);
            if (existing != null)
            {
                return existing;
            }

            tcs = new TaskCompletionSource<MqttPublishedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var waiting = new WaitingSubscription(predicate, tcs);
            _waiters.Add(waiting);

            cts.Token.Register(() =>
            {
                lock (_syncLock)
                {
                    _waiters.Remove(waiting);
                }
                tcs.TrySetCanceled();
            });
        }

        try
        {
            return await tcs.Task;
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException($"Timed out after {timeoutMs}ms waiting for matching MQTT message.");
        }
    }

    public static bool IsTopicMatch(string filter, string topic)
    {
        if (filter == "#" || filter == topic)
        {
            return true;
        }

        var filterParts = filter.Split('/');
        var topicParts = topic.Split('/');

        for (var i = 0; i < filterParts.Length; i++)
        {
            var f = filterParts[i];
            if (f == "#")
            {
                return true;
            }

            if (i >= topicParts.Length)
            {
                return false;
            }

            if (f != "+" && !string.Equals(f, topicParts[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return filterParts.Length == topicParts.Length;
    }

    private sealed class SubscriptionToken : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public SubscriptionToken(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _unsubscribe();
            }
        }
    }

    private sealed record WaitingSubscription(
        Func<MqttPublishedMessage, bool> Predicate,
        TaskCompletionSource<MqttPublishedMessage> Tcs);
}
