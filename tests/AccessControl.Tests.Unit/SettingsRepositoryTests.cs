using AccessControl.Core.DTOs;
using AccessControl.Engine.Services;
using AccessControl.Data.Db;
using AccessControl.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AccessControl.Tests.Unit;

public class SettingsRepositoryTests : IDisposable
{
    private readonly SqliteTestConnectionFactory _connectionFactory;
    private readonly DatabaseSeederService _seeder;
    private readonly SettingsRepository _repository;

    public SettingsRepositoryTests()
    {
        _connectionFactory = new SqliteTestConnectionFactory();
        _seeder = new DatabaseSeederService(_connectionFactory, NullLogger<DatabaseSeederService>.Instance);
        _repository = new SettingsRepository(_connectionFactory);
    }

    public void Dispose() => _connectionFactory.Dispose();

    [Fact]
    public async Task SetAndGetSetting_RoundtripsSuccessfully()
    {
        await _seeder.InitializeAsync();

        await _repository.SetSettingAsync("zwave.transport_type", "WebSocket");
        var val = await _repository.GetSettingAsync("zwave.transport_type");
        Assert.Equal("WebSocket", val);

        var all = await _repository.GetAllSettingsAsync();
        Assert.True(all.ContainsKey("zwave.transport_type"));
        Assert.Equal("WebSocket", all["zwave.transport_type"]);
    }

    [Fact]
    public async Task SystemSettingsService_FallsBackToDefaultsAndEnvVars()
    {
        await _seeder.InitializeAsync();

        var inMemoryConfig = new ConfigurationBuilder().Build();
        var service = new SystemSettingsService(_repository, inMemoryConfig, NullLogger<SystemSettingsService>.Instance);

        var settings = await service.GetSettingsAsync();
        Assert.Equal("WebSocket", settings.ZWaveTransportType);
        Assert.Equal("ws://10.0.0.10:8106", settings.ZWaveWebSocketUrl);
        Assert.Equal("10.0.0.10", settings.MqttHost);
        Assert.Equal(8100, settings.MqttPort);

        // Update settings
        var updated = settings with
        {
            ZWaveTransportType = "Mqtt",
            MqttPort = 1883
        };

        await service.SaveSettingsAsync(updated);

        var reloaded = await service.GetSettingsAsync();
        Assert.Equal("Mqtt", reloaded.ZWaveTransportType);
        Assert.Equal(1883, reloaded.MqttPort);
    }
}
