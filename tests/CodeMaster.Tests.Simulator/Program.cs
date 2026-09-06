using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;

namespace CodeMaster.Tests.Simulator;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var mqttHost = Environment.GetEnvironmentVariable("MQTT_HOST") ?? "localhost";
        var mqttPort = int.TryParse(Environment.GetEnvironmentVariable("MQTT_PORT"), out var port) ? port : 1883;
        var clientId = Environment.GetEnvironmentVariable("CLIENT_ID") ?? $"codemaster-sim-{Guid.NewGuid():N}";

        Console.WriteLine($"[SIMULATOR] Starting CodeMaster Hardware Simulator...");
        Console.WriteLine($"[SIMULATOR] Target MQTT: {mqttHost}:{mqttPort} (ClientId: {clientId})");

        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(mqttHost, mqttPort)
            .WithClientId(clientId)
            .WithCleanSession()
            .Build();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        client.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;
            Console.WriteLine($"[SIMULATOR] Received message: {topic} -> {payload}");

            try
            {
                // 1. Z-Wave Lock Target State Command: zwave/{node}/door_lock/endpoint_{ep}/targetState/set
                if (topic.Contains("/door_lock/") && topic.EndsWith("/targetState/set"))
                {
                    var prefix = topic[..topic.LastIndexOf("/targetState/set", StringComparison.OrdinalIgnoreCase)];
                    var isLock = payload.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

                    Console.WriteLine($"[SIMULATOR] Emulating motor drive: {(isLock ? "LOCKING" : "UNLOCKING")}...");
                    await Task.Delay(50); // Simulate motor physical travel delay

                    var stateTopic = $"{prefix}/currentState";
                    var statePayload = isLock ? "locked" : "unlocked";
                    await client.PublishStringAsync(stateTopic, statePayload);

                    var valueTopic = $"{prefix}/value";
                    var valuePayload = isLock ? "255" : "0";
                    await client.PublishStringAsync(valueTopic, valuePayload);

                    Console.WriteLine($"[SIMULATOR] Published confirmation: {stateTopic} -> {statePayload}");
                }

                // 2. Z-Wave User Code Command: zwave/{node}/user_code/endpoint_{ep}/set
                if (topic.Contains("/user_code/") && topic.EndsWith("/set"))
                {
                    var prefix = topic[..topic.LastIndexOf("/set", StringComparison.OrdinalIgnoreCase)];
                    Console.WriteLine($"[SIMULATOR] Acknowledged user code set: {payload}");

                    var ackTopic = $"{prefix}/status";
                    await client.PublishStringAsync(ackTopic, "{\"status\":\"ok\"}");
                }

                // 3. Simulator Trigger Commands
                if (topic.Equals("codemaster/simulator/command", StringComparison.OrdinalIgnoreCase))
                {
                    using var doc = JsonDocument.Parse(payload);
                    var root = doc.RootElement;
                    var action = root.TryGetProperty("action", out var actProp) ? actProp.GetString() : null;

                    if (string.Equals(action, "disarm", StringComparison.OrdinalIgnoreCase))
                    {
                        var keypadTopic = root.TryGetProperty("keypad", out var kpProp) ? kpProp.GetString() : "ring/keypad";
                        var code = root.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : "4821";

                        var disarmEvent = JsonSerializer.Serialize(new
                        {
                            command = "disarm",
                            code,
                            device_id = keypadTopic
                        });

                        await client.PublishStringAsync(keypadTopic!, disarmEvent);
                        Console.WriteLine($"[SIMULATOR] Injected Ring Keypad disarm: {keypadTopic} -> code {code}");
                    }
                    else if (string.Equals(action, "contact", StringComparison.OrdinalIgnoreCase))
                    {
                        var door = root.TryGetProperty("door", out var doorProp) ? doorProp.GetString() : "front_door";
                        var state = root.TryGetProperty("state", out var stateProp) ? stateProp.GetString() : "closed";
                        var isClosed = string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase);

                        var sensorTopic = $"codemaster/{door}/sensor/state";
                        var sensorPayload = isClosed ? "OFF" : "ON";
                        await client.PublishStringAsync(sensorTopic, sensorPayload);
                        Console.WriteLine($"[SIMULATOR] Injected door sensor state: {sensorTopic} -> {sensorPayload}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SIMULATOR ERROR] Error handling message: {ex.Message}");
            }
        };

        // Resilience loop
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    Console.WriteLine($"[SIMULATOR] Connecting to MQTT broker...");
                    await client.ConnectAsync(options, cts.Token);
                    Console.WriteLine($"[SIMULATOR] Connected to MQTT broker.");

                    // Subscribe to topics
                    await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter("zwave/+/door_lock/+/targetState/set")
                        .WithTopicFilter("zwave/+/user_code/+/set")
                        .WithTopicFilter("codemaster/simulator/command")
                        .WithTopicFilter("codemaster/+/lock/set")
                        .Build(), cts.Token);

                    Console.WriteLine($"[SIMULATOR] Subscribed to lock, user code, and simulator control topics.");

                    // Publish ready heartbeat
                    await client.PublishStringAsync("codemaster/simulator/status", "ready");
                }

                await Task.Delay(1000, cts.Token);
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SIMULATOR] MQTT Connection error: {ex.Message}. Retrying in 2 seconds...");
                try
                {
                    await Task.Delay(2000, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        Console.WriteLine("[SIMULATOR] Shutting down gracefully...");
        if (client.IsConnected)
        {
            await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build());
        }
    }
}
