using CodeMaster.Core.Interfaces;
using CodeMaster.Data.Db;
using CodeMaster.Data.Repositories;
using CodeMaster.Engine.Channels;
using CodeMaster.Engine.Mqtt;
using CodeMaster.Engine.Services;
using CodeMaster.Mcp;
using CodeMaster.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Connection & Database Services
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=codemaster.db";
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(connectionString));
builder.Services.AddScoped<DatabaseSeederService>();

// Repositories
builder.Services.AddScoped<IAccessPointRepository, AccessPointRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICredentialRepository, CredentialRepository>();
builder.Services.AddScoped<IAccessPolicyRepository, AccessPolicyRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IHardwareSlotRepository, HardwareSlotRepository>();

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

// Notifications
builder.Services.Configure<AppriseOptions>(builder.Configuration.GetSection("Apprise"));
builder.Services.AddHttpClient<INotificationDispatcher, AppriseNotificationDispatcher>();

// MCP Services
builder.Services.AddCodeMasterMcp();

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
}

// Forward-auth headers middleware (Remote-User, Remote-Groups)
app.UseMiddleware<ForwardAuthMiddleware>();

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
