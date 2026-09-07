namespace AccessControl.Core.Interfaces;

using System.Diagnostics.CodeAnalysis;
using AccessControl.Core.DTOs;
using AccessControl.Core.Models;

[Flags]
public enum KeypadCapabilities
{
    None = 0,
    ArmDisarm = 1 << 0,
    StatelessPinEvents = 1 << 1,
    SlottedPinStorage = 1 << 2,
    Backlight = 1 << 3,
    BatteryReporting = 1 << 4,

    SupportsArmModes = ArmDisarm,
    SupportsPin = StatelessPinEvents
}

public enum KeypadMode
{
    StatelessEvent,
    HardwareSlotted
}

public interface IKeypadProvider
{
    KeypadCapabilities Capabilities { get; }
    KeypadMode Mode { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    bool TryParseKeypadEvent(MqttInboundMessage message, [NotNullWhen(true)] out KeypadEventDto? keypadEvent);
    bool TryParseKeypadEvent(string topic, string payload, [NotNullWhen(true)] out KeypadEventDto? keypadEvent);
}
