namespace CodeMaster.Core.Transports;

public interface ITransportRegistry
{
    void RegisterTransport(ITransport transport);
    T? GetTransport<T>(string transportId) where T : class, ITransport;
    IReadOnlyList<ITransport> GetAllTransports();
    IEnumerable<T> GetTransports<T>() where T : class, ITransport;
    Task StartAllAsync(CancellationToken ct = default);
    Task StopAllAsync(CancellationToken ct = default);
}
