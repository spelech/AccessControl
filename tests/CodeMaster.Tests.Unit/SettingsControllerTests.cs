using System.Net.Http.Json;
using CodeMaster.Core.DTOs;
using CodeMaster.Web.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CodeMaster.Tests.Unit;

public class SettingsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SettingsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSettings_ReturnsEffectiveConfigurationAndTransports()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/settings");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SettingsResponseDto>();
        Assert.NotNull(result);
        Assert.NotNull(result.Settings);
        Assert.NotEmpty(result.Settings.ZWaveTransportType);
        Assert.NotNull(result.Transports);
        Assert.NotEmpty(result.Transports);
    }

    [Fact]
    public async Task UpdateSettings_SavesAndReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var getResp = await client.GetAsync("/api/settings");
        getResp.EnsureSuccessStatusCode();
        var current = await getResp.Content.ReadFromJsonAsync<SettingsResponseDto>();
        Assert.NotNull(current);

        var updated = current.Settings with
        {
            ZWaveTransportType = "WebSocket",
            ZWaveWebSocketUrl = "ws://10.0.0.10:8106",
            MqttPort = 8100
        };

        var putResp = await client.PutAsJsonAsync("/api/settings", updated);
        putResp.EnsureSuccessStatusCode();

        var verifyResp = await client.GetAsync("/api/settings");
        var verifyResult = await verifyResp.Content.ReadFromJsonAsync<SettingsResponseDto>();
        Assert.NotNull(verifyResult);
        Assert.Equal("ws://10.0.0.10:8106", verifyResult.Settings.ZWaveWebSocketUrl);
        Assert.Equal(8100, verifyResult.Settings.MqttPort);
    }

    [Fact]
    public async Task TestConnection_UnsupportedTransport_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var req = new TestConnectionRequestDto("InvalidTransport");
        var resp = await client.PostAsJsonAsync("/api/settings/test-connection", req);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
