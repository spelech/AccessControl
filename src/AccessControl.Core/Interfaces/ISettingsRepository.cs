namespace AccessControl.Core.Interfaces;

public interface ISettingsRepository
{
    Task<IReadOnlyDictionary<string, string>> GetAllSettingsAsync(CancellationToken ct = default);
    Task<string?> GetSettingAsync(string key, CancellationToken ct = default);
    Task SetSettingAsync(string key, string value, CancellationToken ct = default);
    Task SetSettingsAsync(IDictionary<string, string> settings, CancellationToken ct = default);
}
