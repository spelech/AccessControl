using AccessControl.Core.Interfaces;
using AccessControl.Core.Transports;
using AccessControl.Data.Db;
using AccessControl.Data.Repositories;
using AccessControl.Engine.Channels;
using AccessControl.Engine.Mqtt;
using AccessControl.Engine.Security;
using AccessControl.Engine.Services;
using AccessControl.Engine.Transports;
using AccessControl.Mcp;
using AccessControl.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Home Assistant Add-on options ingestion
const string haOptionsPath = "/data/options.json";
if (File.Exists(haOptionsPath))
{
    try
    {
        using var stream = File.OpenRead(haOptionsPath);
        using var doc = System.Text.Json.JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var inMemoryConfig = new Dictionary<string, string?>();
        if (root.TryGetProperty("mqtt_host", out var h) && !string.IsNullOrWhiteSpace(h.GetString()))
            inMemoryConfig["Mqtt:Host"] = h.GetString();
        if (root.TryGetProperty("mqtt_port", out var p))
            inMemoryConfig["Mqtt:Port"] = p.GetInt32().ToString();
        if (root.TryGetProperty("mqtt_username", out var u) && !string.IsNullOrWhiteSpace(u.GetString()))
            inMemoryConfig["Mqtt:Username"] = u.GetString();
        if (root.TryGetProperty("mqtt_password", out var pw) && !string.IsNullOrWhiteSpace(pw.GetString()))
            inMemoryConfig["Mqtt:Password"] = pw.GetString();

        builder.Configuration.AddInMemoryCollection(inMemoryConfig);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Warning] Failed to parse {haOptionsPath}: {ex.Message}");
    }
}

// Connection & Database Services
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=accesscontrol.db";
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(connectionString));
builder.Services.AddScoped<DatabaseSeederService>();

// Repositories
builder.Services.AddScoped<IAccessPointRepository, AccessPointRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICredentialRepository, CredentialRepository>();
builder.Services.AddScoped<IAccessPolicyRepository, AccessPolicyRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IHardwareSlotRepository, HardwareSlotRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<SystemSettingsService>();
builder.Services.AddSingleton<ITransportRegistry, TransportRegistry>();

// MQTT Channel & Client Services
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddSingleton<IMqttInboundChannel, MqttInboundChannel>();
builder.Services.AddSingleton<MqttClientService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttClientService>());
builder.Services.AddSingleton<IMqttClientService>(sp => sp.GetRequiredService<MqttClientService>());
builder.Services.AddHostedService<MqttInboundConsumerService>();

// Core Engine Services
builder.Services.AddScoped<IDoorOperationService, DoorOperationService>();
builder.Services.AddScoped<IAccessPolicyEvaluator, AccessPolicyEvaluator>();
builder.Services.AddScoped<IHardwareSlotSyncWorker, HardwareSlotSyncWorker>();
builder.Services.AddSingleton<IMqttDiscoveryService, MqttTopicDiscoveryService>();
builder.Services.AddSingleton<IHomeAssistantDiscoveryService, HomeAssistantDiscoveryService>();
builder.Services.AddSingleton<IAccessEventBroadcaster, AccessEventBroadcaster>();
builder.Services.AddSingleton<ICredentialEncryptionService, AesGcmCredentialEncryptionService>();

// Notifications
builder.Services.Configure<AppriseOptions>(builder.Configuration.GetSection("Apprise"));
builder.Services.AddHttpClient<INotificationDispatcher, AppriseNotificationDispatcher>();

// MCP Services
builder.Services.AddAccessControlMcp();

// Controllers & JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

var app = builder.Build();

// Run Database Seeder on startup
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeederService>();
    await seeder.InitializeAsync();

    try
    {
        var settingsService = scope.ServiceProvider.GetRequiredService<SystemSettingsService>();
        var settings = await settingsService.GetSettingsAsync();

        var registry = scope.ServiceProvider.GetRequiredService<ITransportRegistry>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var mqtt = scope.ServiceProvider.GetService<IMqttClientService>();

        registry.RegisterTransport(new ZWaveMqttTransport(mqtt, settings.ZWaveMqttPrefix, loggerFactory.CreateLogger<ZWaveMqttTransport>()));

        if (settings.ZWaveTransportType.Equals("WebSocket", StringComparison.OrdinalIgnoreCase))
        {
            var wsTransport = new ZWaveWebSocketTransport(settings.ZWaveWebSocketUrl, loggerFactory.CreateLogger<ZWaveWebSocketTransport>());
            registry.RegisterTransport(wsTransport);
            _ = wsTransport.StartAsync(CancellationToken.None);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Warning] Failed to initialize hardware transports: {ex.Message}");
    }
}

// Forward-auth headers middleware (Remote-User, Remote-Groups)
app.UseMiddleware<ForwardAuthMiddleware>();

// Home Assistant Ingress Dynamic BasePath & HTML Injection
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var isHtmlRequest = path == "/" || path == "/index.html" || 
        (!Path.HasExtension(path) && !path.StartsWith("/api") && !path.StartsWith("/mcp") && !path.StartsWith("/health"));

    if (isHtmlRequest)
    {
        var ingressPath = context.Request.Headers["X-Ingress-Path"].FirstOrDefault()?.TrimEnd('/');
        var wwwroot = app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var indexPath = Path.Combine(wwwroot, "index.html");

        if (File.Exists(indexPath))
        {
            var html = await File.ReadAllTextAsync(indexPath);
            if (!string.IsNullOrEmpty(ingressPath))
            {
                html = html.Replace("<meta name=\"base-path\" content=\"\" />", 
                    $"<meta name=\"base-path\" content=\"{ingressPath}\" />\n    <base href=\"{ingressPath}/\" />");
            }
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
            return;
        }
    }

    await next();
});

// Static files for SPA dashboard
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

// Health check probe
app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.1.0" 
}));

// Controllers & MCP Endpoints
app.MapControllers();
app.MapMcpEndpoints();

// SPA fallback
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
