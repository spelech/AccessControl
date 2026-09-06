using CodeMaster.Core.Models;
using CodeMaster.Engine.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;

namespace CodeMaster.Engine.Mqtt;

/// <summary>
/// Background service maintaining resilient MQTT connectivity and routing inbound messages to IMqttInboundChannel.
/// </summary>
public sealed class MqttClientService : BackgroundService, IMqttClientService
{
    private readonly MqttOptions _options;
    private readonly IMqttInboundChannel _inboundChannel;
    private readonly ILogger<MqttClientService> _logger;
    private readonly IMqttClient _client;
    private readonly bool _ownsClient;
    private TaskCompletionSource<bool> _disconnectTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public MqttClientService(
        IOptions<MqttOptions> options,
        IMqttInboundChannel inboundChannel,
        ILogger<MqttClientService> logger,
        IMqttClient? mqttClient = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _inboundChannel = inboundChannel ?? throw new ArgumentNullException(nameof(inboundChannel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (mqttClient is not null)
        {
            _client = mqttClient;
            _ownsClient = false;
        }
        else
        {
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();
            _ownsClient = true;
        }

        _client.ApplicationMessageReceivedAsync += HandleIncomingMessageAsync;
        _client.DisconnectedAsync += HandleDisconnectedAsync;
    }

    /// <inheritdoc />
    public bool IsConnected => _client.IsConnected;

    /// <inheritdoc />
    public async Task PublishAsync(string topic, string payload, bool retain = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(payload);

        if (!_client.IsConnected)
        {
            throw new InvalidOperationException($"Cannot publish to topic '{topic}': MQTT client is not connected.");
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithRetainFlag(retain)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.PublishAsync(message, ct);
    }

    /// <summary>
    /// Handles incoming application messages from MQTTnet by writing them to the inbound channel.
    /// </summary>
    public Task HandleIncomingMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage?.Topic ?? string.Empty;
        var payload = e.ApplicationMessage?.ConvertPayloadToString() ?? string.Empty;
        var inboundMessage = new MqttInboundMessage(topic, payload, DateTimeOffset.UtcNow);

        if (!_inboundChannel.Writer.TryWrite(inboundMessage))
        {
            _logger.LogWarning("Inbound MQTT channel capacity reached. Queueing asynchronously for topic '{Topic}'", topic);
            _ = _inboundChannel.Writer.WriteAsync(inboundMessage).AsTask();
        }

        return Task.CompletedTask;
    }

    private Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        _logger.LogWarning("Disconnected from MQTT broker: {Reason} {ReasonString}", e.Reason, e.ReasonString);
        _disconnectTcs.TrySetResult(true);
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reconnectDelay = TimeSpan.FromSeconds(Math.Max(1, _options.InitialReconnectDelaySeconds));
        var maxDelay = TimeSpan.FromSeconds(Math.Max(1, _options.MaxReconnectDelaySeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    _logger.LogInformation("Connecting to MQTT broker at {Host}:{Port} (ClientId={ClientId})...",
                        _options.Host, _options.Port, _options.ClientId);

                    var clientOptions = BuildMqttClientOptions();
                    await _client.ConnectAsync(clientOptions, stoppingToken);

                    _logger.LogInformation("Successfully connected to MQTT broker at {Host}:{Port}", _options.Host, _options.Port);

                    await SubscribeToTopicsAsync(stoppingToken);

                    // Reset backoff delay upon successful connection & subscription
                    reconnectDelay = TimeSpan.FromSeconds(Math.Max(1, _options.InitialReconnectDelaySeconds));
                    _disconnectTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                // Wait until disconnected or cancellation requested
                await _disconnectTcs.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MQTT connection error. Retrying in {Delay}s...", reconnectDelay.TotalSeconds);
                try
                {
                    await Task.Delay(reconnectDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var nextDelaySeconds = Math.Min(reconnectDelay.TotalSeconds * 2, maxDelay.TotalSeconds);
                reconnectDelay = TimeSpan.FromSeconds(nextDelaySeconds);
            }
        }
    }

    private async Task SubscribeToTopicsAsync(CancellationToken ct)
    {
        if (_options.SubscribedTopics.Count == 0)
        {
            return;
        }

        var subscribeBuilder = new MqttClientSubscribeOptionsBuilder();
        foreach (var topic in _options.SubscribedTopics)
        {
            if (!string.IsNullOrWhiteSpace(topic))
            {
                subscribeBuilder.WithTopicFilter(topic);
            }
        }

        var subscribeOptions = subscribeBuilder.Build();
        if (subscribeOptions.TopicFilters.Count > 0)
        {
            _logger.LogInformation("Subscribing to {Count} MQTT topics: {Topics}",
                subscribeOptions.TopicFilters.Count,
                string.Join(", ", subscribeOptions.TopicFilters.Select(f => f.Topic)));

            await _client.SubscribeAsync(subscribeOptions, ct);
        }
    }

    private MqttClientOptions BuildMqttClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId(_options.ClientId)
            .WithCleanSession(_options.CleanSession);

        if (!string.IsNullOrEmpty(_options.Username))
        {
            builder.WithCredentials(_options.Username, _options.Password);
        }

        if (_options.UseTls)
        {
            builder.WithTlsOptions(o => o.UseTls());
        }

        return builder.Build();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_client.IsConnected)
            {
                var disconnectOptions = new MqttClientDisconnectOptionsBuilder()
                    .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                    .Build();

                await _client.DisconnectAsync(disconnectOptions, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while disconnecting MQTT client during shutdown");
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
        base.Dispose();
    }
}
