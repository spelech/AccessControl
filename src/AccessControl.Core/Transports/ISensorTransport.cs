using AccessControl.Core.Models;

namespace AccessControl.Core.Transports;

public interface ISensorTransport : ITransport
{
    event Action<DoorSensorStateEventArgs>? OnSensorStateChanged;
    Task<DoorContactState> GetSensorStateAsync(string deviceTarget, CancellationToken ct = default);
}
