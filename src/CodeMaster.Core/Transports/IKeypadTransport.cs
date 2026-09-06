namespace CodeMaster.Core.Transports;

public interface IKeypadTransport : ITransport
{
    event Action<KeypadEntryEventArgs>? OnKeypadEntry;
    Task<bool> SetKeypadModeAsync(string deviceTarget, KeypadArmMode mode, CancellationToken ct = default);
}
