using System.Net;
using System.Text.Json;
using CodeMaster.Core.Models;
using CodeMaster.Engine.Services;
using Xunit;

namespace CodeMaster.Tests.Unit;

public class AppriseNotificationDispatcherTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(StatusCode);
        }
    }

    [Fact]
    public async Task DispatchAlertAsync_PostsJsonPayloadToAppriseUrl()
    {
        var handler = new MockHttpMessageHandler();
        using var client = new HttpClient(handler);
        var targetUrl = "http://10.0.0.10:8000/notify/apprise";
        var dispatcher = new AppriseNotificationDispatcher(client, targetUrl);

        await dispatcher.DispatchAlertAsync("Test Title", "Test Message");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal(targetUrl, handler.LastRequest.RequestUri?.ToString());

        Assert.NotNull(handler.LastRequestBody);
        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        Assert.Equal("Test Title", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("Test Message", doc.RootElement.GetProperty("body").GetString());
    }

    [Fact]
    public async Task DispatchAccessEventAsync_FormatsNotificationDetails()
    {
        var handler = new MockHttpMessageHandler();
        using var client = new HttpClient(handler);
        var dispatcher = new AppriseNotificationDispatcher(client, "http://10.0.0.10:8000/notify/apprise");

        var log = new AccessLog
        {
            AccessPointId = "front_door",
            UserName = "Steve",
            EventType = AccessEventType.Unlocked,
            Method = AccessMethod.RingKeypad,
            Details = "PIN 4821 verified"
        };

        await dispatcher.DispatchAccessEventAsync(log);

        Assert.NotNull(handler.LastRequestBody);
        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        var title = doc.RootElement.GetProperty("title").GetString();
        var body = doc.RootElement.GetProperty("body").GetString();

        Assert.Contains("Unlocked", title);
        Assert.Contains("Steve", title);
        Assert.Contains("front_door", body);
        Assert.Contains("RingKeypad", body);
        Assert.Contains("PIN 4821 verified", body);
    }

    [Fact]
    public async Task DispatchAlertAsync_WhenDisabled_DoesNotSendRequest()
    {
        var handler = new MockHttpMessageHandler();
        using var client = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new AppriseOptions
        {
            Url = "http://10.0.0.10:8000/notify/apprise",
            Enabled = false
        });
        var dispatcher = new AppriseNotificationDispatcher(client, options);

        await dispatcher.DispatchAlertAsync("Title", "Body");

        Assert.Null(handler.LastRequest);
    }
}
