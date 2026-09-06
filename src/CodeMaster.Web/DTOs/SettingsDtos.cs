using CodeMaster.Core.DTOs;

namespace CodeMaster.Web.DTOs;

public record TransportStatusDto(
    string TransportId,
    string DisplayName,
    string Status,
    bool IsConnected,
    string? Details = null
);

public record SettingsResponseDto(
    SystemSettingsDto Settings,
    IReadOnlyList<TransportStatusDto> Transports
);

public record TestConnectionRequestDto(
    string TransportType,
    string? EndpointUrl = null,
    string? MqttHost = null,
    int? MqttPort = null
);

public record ZWaveNodeSummaryDto(
    int NodeId,
    string Name,
    string DeviceType,
    string? Model = null
);

public record TestConnectionResponseDto(
    bool Success,
    long LatencyMs,
    string? DriverVersion = null,
    string? ServerVersion = null,
    int? NodeCount = null,
    IReadOnlyList<ZWaveNodeSummaryDto>? DetectedNodes = null,
    string? Message = null
);
