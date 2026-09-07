namespace AccessControl.Core.Transports;

public interface ITransport
{
    string TransportId { get; }
    string DisplayName { get; }
    bool IsConnected { get; }
    TransportStatus Status { get; }

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);

    event Action<TransportStatusChangedEventArgs>? OnStatusChanged;
}
