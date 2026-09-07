using System.Text;
using System.Text.Json;
using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AccessControl.Engine.Services;

public class AppriseNotificationDispatcher : INotificationDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly AppriseOptions _options;
    private readonly ILogger<AppriseNotificationDispatcher>? _logger;

    [ActivatorUtilitiesConstructor]
    public AppriseNotificationDispatcher(
        HttpClient httpClient,
        IOptions<AppriseOptions> options,
        ILogger<AppriseNotificationDispatcher>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new AppriseOptions();
        _logger = logger;
    }

    public AppriseNotificationDispatcher(
        HttpClient httpClient,
        string url,
        ILogger<AppriseNotificationDispatcher>? logger = null)
        : this(httpClient, Options.Create(new AppriseOptions { Url = url }), logger)
    {
    }

    public async Task DispatchAlertAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Url))
        {
            _logger?.LogDebug("Apprise dispatch skipped: disabled or no URL configured.");
            return;
        }

        try
        {
            var payload = new
            {
                title,
                body = message,
                type = "info"
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_options.Url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Apprise notification failed with HTTP status code {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to dispatch Apprise notification to {Url}", _options.Url);
        }
    }

    public Task DispatchAccessEventAsync(AccessLog log, CancellationToken cancellationToken = default)
    {
        var title = $"[CodeMaster] {log.EventType}: {log.UserName ?? "Unknown User"}";
        var body = $"Access Point: {log.AccessPointId}\nEvent: {log.EventType}\nUser: {log.UserName ?? "Unknown"}\nMethod: {log.Method}\nTimestamp: {log.Timestamp:O}" +
                   (!string.IsNullOrEmpty(log.Details) ? $"\nDetails: {log.Details}" : "");

        return DispatchAlertAsync(title, body, cancellationToken);
    }
}
