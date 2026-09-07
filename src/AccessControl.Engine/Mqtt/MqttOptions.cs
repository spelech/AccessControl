namespace AccessControl.Engine.Mqtt;

/// <summary>
/// Configuration options for connecting to an MQTT broker.
/// </summary>
public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    /// <summary>
    /// Hostname or IP of the MQTT broker. Defaults to "localhost".
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Port of the MQTT broker. Defaults to 1883.
    /// </summary>
    public int Port { get; set; } = 1883;

    /// <summary>
    /// Client ID used for MQTT connections. Defaults to "accesscontrol".
    /// </summary>
    public string ClientId { get; set; } = "accesscontrol";

    /// <summary>
    /// Optional username for MQTT authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Optional password for MQTT authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Whether to establish connection over TLS. Defaults to false.
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Whether to use clean session. Defaults to true.
    /// </summary>
    public bool CleanSession { get; set; } = true;

    /// <summary>
    /// Topics to automatically subscribe to upon successful connection.
    /// </summary>
    public List<string> SubscribedTopics { get; set; } =
    [
        "zwave/#",
        "ring/#",
        "zigbee2mqtt/#",
        "homeassistant/#",
        "accesscontrol/#",
        "codemaster/#"
    ];

    /// <summary>
    /// Initial delay in seconds before attempting reconnection on disconnect.
    /// </summary>
    public int InitialReconnectDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Maximum backoff delay in seconds for reconnection attempts.
    /// </summary>
    public int MaxReconnectDelaySeconds { get; set; } = 30;
}
