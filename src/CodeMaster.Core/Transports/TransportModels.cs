using CodeMaster.Core.DTOs;
using CodeMaster.Core.Models;

namespace CodeMaster.Core.Transports;

public enum TransportStatus
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Degraded = 3
}

public enum ZWaveTransportType
{
    WebSocket = 0,
    Mqtt = 1
}

public enum KeypadArmMode
{
    Disarmed = 0,
    ArmedStay = 1,
    ArmedAway = 2
}

public record TransportStatusChangedEventArgs(
    string TransportId,
    TransportStatus OldStatus,
    TransportStatus NewStatus,
    string? ErrorMessage = null
);

public record LockStateUpdatedEventArgs(
    string DeviceTarget,
    LockState State,
    string? Source = null
);

public record KeypadEntryEventArgs(
    string DeviceTarget,
    string Pin,
    string? Action = null,
    DateTime Timestamp = default
);

public record DoorSensorStateEventArgs(
    string DeviceTarget,
    DoorContactState State,
    DateTime Timestamp = default
);
