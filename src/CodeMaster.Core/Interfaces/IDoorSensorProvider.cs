namespace CodeMaster.Core.Interfaces;

using System.Diagnostics.CodeAnalysis;
using CodeMaster.Core.Models;

public interface IDoorSensorProvider
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    bool TryParseContactEvent(MqttInboundMessage message, [NotNullWhen(true)] out DoorContactState? contactState);
    bool TryParseContactEvent(string topic, string payload, [NotNullWhen(true)] out DoorContactState? contactState);
}
