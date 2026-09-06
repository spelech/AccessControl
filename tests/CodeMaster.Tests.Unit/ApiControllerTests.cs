using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeMaster.Core.Models;
using CodeMaster.Data.Db;
using CodeMaster.Mcp.Protocol;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeMaster.Tests.Unit;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;

    public TestWebApplicationFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"codemaster_test_{Guid.NewGuid():N}.db");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbConnectionFactory));
            if (dbDescriptor != null)
            {
                services.Remove(dbDescriptor);
            }

            services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory($"Data Source={_dbPath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}

public class ApiControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeederService>();
        seeder.InitializeAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task HealthProbe_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var version = content.GetProperty("version").GetString();
        Assert.NotNull(version);
        Assert.StartsWith("1.", version);
    }

    [Fact]
    public async Task AccessPointsController_CrudAndLockUnlockOperations_Succeed()
    {
        // 1. Create Door
        var newDoor = new AccessPoint
        {
            Name = "Front Door Test",
            LockProviderType = "AugustZWave",
            LockConfigJson = "{\"topic\":\"zwave/front_door\"}",
            AutoLockEnabled = true,
            AutoLockDaySeconds = 300,
            AutoLockNightSeconds = 60
        };

        var createRes = await _client.PostAsJsonAsync("/api/doors", newDoor);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<AccessPoint>();
        Assert.NotNull(created);
        Assert.Equal("Front Door Test", created!.Name);

        // 2. Get All Doors
        var listRes = await _client.GetAsync("/api/doors");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var doors = await listRes.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(doors);
        Assert.Contains(doors!, d => d.GetProperty("id").GetString() == created.Id);

        // 3. Unlock Door
        var unlockRes = await _client.PostAsync($"/api/doors/{created.Id}/unlock", null);
        Assert.Equal(HttpStatusCode.OK, unlockRes.StatusCode);
        var unlockJson = await unlockRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unlocked", unlockJson.GetProperty("lockState").GetString());

        // 4. Lock Door
        var lockRes = await _client.PostAsync($"/api/doors/{created.Id}/lock", null);
        Assert.Equal(HttpStatusCode.OK, lockRes.StatusCode);
        var lockJson = await lockRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Locked", lockJson.GetProperty("lockState").GetString());

        // 5. Delete Door
        var deleteRes = await _client.DeleteAsync($"/api/doors/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // 6. Verify Deleted
        var getDeleted = await _client.GetAsync($"/api/doors/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getDeleted.StatusCode);
    }

    [Fact]
    public async Task UsersController_CreateUserWithPinAndCredentials_Succeeds()
    {
        var userRequest = new
        {
            name = "Alice Test",
            role = "Member",
            pin = "54321",
            pinLabel = "Alice Master PIN"
        };

        var createRes = await _client.PostAsJsonAsync("/api/users", userRequest);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        var createdUser = await createRes.Content.ReadFromJsonAsync<User>(jsonOptions);
        Assert.NotNull(createdUser);
        Assert.Equal("Alice Test", createdUser!.Name);

        // Verify credentials
        var credsRes = await _client.GetAsync($"/api/users/{createdUser.Id}/credentials");
        Assert.Equal(HttpStatusCode.OK, credsRes.StatusCode);
        var creds = await credsRes.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(creds);
        Assert.Single(creds!);
        Assert.Equal(5, creds![0].GetProperty("pinLength").GetInt32());

        // Update PIN
        var updatePinRes = await _client.PostAsJsonAsync($"/api/users/{createdUser.Id}/credentials/pin", new
        {
            pin = "9876",
            label = "Updated PIN"
        });
        Assert.Equal(HttpStatusCode.OK, updatePinRes.StatusCode);
        var updatedCred = await updatePinRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, updatedCred.GetProperty("pinLength").GetInt32());

        // Attempt invalid PINs (non-numeric, too short, too long)
        var invalidShortRes = await _client.PostAsJsonAsync($"/api/users/{createdUser.Id}/credentials/pin", new
        {
            pin = "12",
            label = "Too short"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidShortRes.StatusCode);
        var shortJson = await invalidShortRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("PIN must be between 4 and 8 numeric digits", shortJson.GetProperty("error").GetString());

        var invalidAlphaRes = await _client.PostAsJsonAsync($"/api/users/{createdUser.Id}/credentials/pin", new
        {
            pin = "abcd1",
            label = "Non-numeric"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidAlphaRes.StatusCode);
    }

    [Fact]
    public async Task UsersController_SavePolicy_AssignsPolicyToDoorsAndUser()
    {
        var jsonOpts = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        // 1. Create Door
        var doorRes = await _client.PostAsJsonAsync("/api/doors", new AccessPoint
        {
            Name = "Side Garage Door",
            LockProviderType = "GenericMqtt"
        });
        var door = await doorRes.Content.ReadFromJsonAsync<AccessPoint>(jsonOpts);
        Assert.NotNull(door);

        // 2. Create User
        var userRes = await _client.PostAsJsonAsync("/api/users", new
        {
            name = "Bob Contractor",
            role = "Service"
        });
        var user = await userRes.Content.ReadFromJsonAsync<User>(jsonOpts);
        Assert.NotNull(user);

        // 3. Save Policy for user
        var policy = new AccessPolicy
        {
            Name = "Weekday Business Hours",
            ScheduleType = ScheduleType.WeeklyRecurring,
            DaysOfWeek = 31,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            DoorIds = [door!.Id]
        };

        var savePolicyRes = await _client.PostAsJsonAsync($"/api/users/{user!.Id}/policies", policy);
        var errBody = await savePolicyRes.Content.ReadAsStringAsync();
        Assert.True(savePolicyRes.IsSuccessStatusCode, $"Failed with {savePolicyRes.StatusCode}: {errBody}");

        // 4. Retrieve policies for user - must NOT be empty!
        var getPoliciesRes = await _client.GetAsync($"/api/users/{user.Id}/policies");
        Assert.Equal(HttpStatusCode.OK, getPoliciesRes.StatusCode);
        var userPolicies = await getPoliciesRes.Content.ReadFromJsonAsync<List<AccessPolicy>>(jsonOpts);
        Assert.NotNull(userPolicies);
        Assert.NotEmpty(userPolicies!);
        Assert.Equal("Weekday Business Hours", userPolicies![0].Name);
    }

    [Fact]
    public async Task DiscoveryController_ReturnsDiscoveredTopics()
    {
        var response = await _client.GetAsync("/api/discovery/mqtt");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var topics = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(topics);
        Assert.NotEmpty(topics!);
        Assert.Contains(topics!, t => t.GetProperty("deviceType").GetString() == "lock");
    }

    [Fact]
    public async Task AccessLogsController_RetrievesRecentLogs()
    {
        // 1. Create a dedicated test door
        var doorRes = await _client.PostAsJsonAsync("/api/doors", new AccessPoint
        {
            Name = "Audit Log Test Door",
            LockProviderType = "GenericMqtt"
        });
        var door = await doorRes.Content.ReadFromJsonAsync<AccessPoint>();
        Assert.NotNull(door);

        // 2. Perform an unlock operation to record a genuine audit log entry
        var unlockRes = await _client.PostAsync($"/api/doors/{door!.Id}/unlock", null);
        Assert.Equal(HttpStatusCode.OK, unlockRes.StatusCode);

        // 3. Query logs specifically filtered by this door's ID
        var response = await _client.GetAsync($"/api/logs?doorId={door.Id}&limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonOpts = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        var logs = await response.Content.ReadFromJsonAsync<List<AccessLog>>(jsonOpts);
        Assert.NotNull(logs);
        Assert.NotEmpty(logs!);
        var log = logs!.First();
        Assert.Equal(door.Id, log.AccessPointId);
        Assert.Equal(AccessEventType.Unlocked, log.EventType);
        Assert.Equal(AccessMethod.Manual, log.Method);
    }

    [Fact]
    public async Task McpServer_SseEndpoint_ReturnsEndpointEvent()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp/sse");
        request.Headers.Add("Accept", "text/event-stream");

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);
        var firstLine = await reader.ReadLineAsync(cts.Token);
        var secondLine = await reader.ReadLineAsync(cts.Token);

        Assert.Equal("event: endpoint", firstLine);
        Assert.NotNull(secondLine);
        Assert.StartsWith("data: /mcp/messages?sessionId=", secondLine!);
    }

    [Fact]
    public async Task McpServer_ProtocolVersionNegotiation_HandlesDefaultAndFallback()
    {
        // 1. Default (no version or 2026 version) -> returns 2026-07-28
        var initRequestDefault = new McpRpcRequest
        {
            Id = 1,
            Method = "initialize",
            Params = JsonSerializer.SerializeToElement(new { protocolVersion = "2026-07-28" })
        };

        var postRes1 = await _client.PostAsJsonAsync("/mcp/messages", initRequestDefault);
        Assert.Equal(HttpStatusCode.OK, postRes1.StatusCode);
        var rpcRes1 = await postRes1.Content.ReadFromJsonAsync<McpRpcResponse>();
        Assert.NotNull(rpcRes1?.Result);

        var resDoc1 = JsonSerializer.SerializeToElement(rpcRes1!.Result);
        Assert.Equal("2026-07-28", resDoc1.GetProperty("protocolVersion").GetString());

        // 2. Fallback request for 2024-11-05 -> returns 2024-11-05
        var initRequestLegacy = new McpRpcRequest
        {
            Id = 2,
            Method = "initialize",
            Params = JsonSerializer.SerializeToElement(new { protocolVersion = "2024-11-05" })
        };

        var postRes2 = await _client.PostAsJsonAsync("/mcp/messages", initRequestLegacy);
        Assert.Equal(HttpStatusCode.OK, postRes2.StatusCode);
        var rpcRes2 = await postRes2.Content.ReadFromJsonAsync<McpRpcResponse>();
        Assert.NotNull(rpcRes2?.Result);

        var resDoc2 = JsonSerializer.SerializeToElement(rpcRes2!.Result);
        Assert.Equal("2024-11-05", resDoc2.GetProperty("protocolVersion").GetString());
    }

    [Fact]
    public async Task McpServer_ToolsList_ReturnsAllSixTools()
    {
        var request = new McpRpcRequest
        {
            Id = 3,
            Method = "tools/list"
        };

        var postRes = await _client.PostAsJsonAsync("/mcp/messages", request);
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);
        var rpcRes = await postRes.Content.ReadFromJsonAsync<McpRpcResponse>();
        Assert.NotNull(rpcRes?.Result);

        var doc = JsonSerializer.SerializeToElement(rpcRes!.Result);
        var tools = doc.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();

        Assert.Contains("codemaster__list_doors", tools);
        Assert.Contains("codemaster__unlock_door", tools);
        Assert.Contains("codemaster__lock_door", tools);
        Assert.Contains("codemaster__create_guest_pin", tools);
        Assert.Contains("codemaster__revoke_user", tools);
        Assert.Contains("codemaster__get_access_logs", tools);
    }

    [Fact]
    public async Task McpServer_ToolExecution_CreateGuestPinAndRevokeUser_Succeeds()
    {
        // 1. Create a door for guest assignment
        var door = new AccessPoint { Name = "Guest Patio Door", LockProviderType = "GenericMqtt" };
        var doorRes = await _client.PostAsJsonAsync("/api/doors", door);
        var createdDoor = await doorRes.Content.ReadFromJsonAsync<AccessPoint>();
        Assert.NotNull(createdDoor);

        // 2. Call codemaster__create_guest_pin
        var createGuestCall = new McpRpcRequest
        {
            Id = 4,
            Method = "tools/call",
            Params = JsonSerializer.SerializeToElement(new
            {
                name = "codemaster__create_guest_pin",
                arguments = new
                {
                    name = "Dog Walker",
                    pin = "4321",
                    validFrom = DateTime.UtcNow.ToString("o"),
                    validUntil = DateTime.UtcNow.AddDays(2).ToString("o"),
                    doorIds = new[] { createdDoor!.Id }
                }
            })
        };

        var postRes = await _client.PostAsJsonAsync("/mcp/messages", createGuestCall);
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);
        var rpcRes = await postRes.Content.ReadFromJsonAsync<McpRpcResponse>();
        Assert.NotNull(rpcRes?.Result);

        var resultDoc = JsonSerializer.SerializeToElement(rpcRes!.Result);
        Assert.False(resultDoc.GetProperty("isError").GetBoolean());
        var contentText = resultDoc.GetProperty("content")[0].GetProperty("text").GetString();
        Assert.NotNull(contentText);

        using var guestDoc = JsonDocument.Parse(contentText!);
        var guestUserId = guestDoc.RootElement.GetProperty("userId").GetString();
        Assert.NotNull(guestUserId);

        // 3. Call codemaster__revoke_user
        var revokeCall = new McpRpcRequest
        {
            Id = 5,
            Method = "tools/call",
            Params = JsonSerializer.SerializeToElement(new
            {
                name = "codemaster__revoke_user",
                arguments = new
                {
                    userId = guestUserId
                }
            })
        };

        var revokeRes = await _client.PostAsJsonAsync("/mcp/messages", revokeCall);
        Assert.Equal(HttpStatusCode.OK, revokeRes.StatusCode);
        var revokeRpcRes = await revokeRes.Content.ReadFromJsonAsync<McpRpcResponse>();
        Assert.NotNull(revokeRpcRes?.Result);

        var revokeResultDoc = JsonSerializer.SerializeToElement(revokeRpcRes!.Result);
        Assert.False(revokeResultDoc.GetProperty("isError").GetBoolean());
        Assert.Contains("revoked successfully", revokeResultDoc.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task McpServer_ToolExecution_ListDoorsAndLockUnlock_Succeeds()
    {
        // 1. Create door
        var door = new AccessPoint { Name = "Garage Entry", LockProviderType = "GenericMqtt" };
        var doorRes = await _client.PostAsJsonAsync("/api/doors", door);
        var createdDoor = await doorRes.Content.ReadFromJsonAsync<AccessPoint>();
        Assert.NotNull(createdDoor);

        // 2. Call codemaster__list_doors
        var listCall = new McpRpcRequest
        {
            Id = 6,
            Method = "tools/call",
            Params = JsonSerializer.SerializeToElement(new { name = "codemaster__list_doors" })
        };
        var listRes = await _client.PostAsJsonAsync("/mcp/messages", listCall);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var listRpcRes = await listRes.Content.ReadFromJsonAsync<McpRpcResponse>();
        Assert.NotNull(listRpcRes?.Result);
        var listText = JsonSerializer.SerializeToElement(listRpcRes!.Result).GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Contains("Garage Entry", listText);

        // 3. Call codemaster__unlock_door
        var unlockCall = new McpRpcRequest
        {
            Id = 7,
            Method = "tools/call",
            Params = JsonSerializer.SerializeToElement(new
            {
                name = "codemaster__unlock_door",
                arguments = new { doorId = createdDoor!.Id, durationMinutes = 5 }
            })
        };
        var unlockRes = await _client.PostAsJsonAsync("/mcp/messages", unlockCall);
        Assert.Equal(HttpStatusCode.OK, unlockRes.StatusCode);
        var unlockRpcRes = await unlockRes.Content.ReadFromJsonAsync<McpRpcResponse>();
        Assert.NotNull(unlockRpcRes?.Result);
        var unlockText = JsonSerializer.SerializeToElement(unlockRpcRes!.Result).GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Contains("unlocked", unlockText);

        // 4. Call codemaster__lock_door
        var lockCall = new McpRpcRequest
        {
            Id = 8,
            Method = "tools/call",
            Params = JsonSerializer.SerializeToElement(new
            {
                name = "codemaster__lock_door",
                arguments = new { doorId = createdDoor!.Id }
            })
        };
        var lockRes = await _client.PostAsJsonAsync("/mcp/messages", lockCall);
        Assert.Equal(HttpStatusCode.OK, lockRes.StatusCode);
        var lockRpcRes = await lockRes.Content.ReadFromJsonAsync<McpRpcResponse>();
        Assert.NotNull(lockRpcRes?.Result);
        var lockText = JsonSerializer.SerializeToElement(lockRpcRes!.Result).GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Contains("locked", lockText);

        // 5. Call codemaster__get_access_logs
        var logsCall = new McpRpcRequest
        {
            Id = 9,
            Method = "tools/call",
            Params = JsonSerializer.SerializeToElement(new
            {
                name = "codemaster__get_access_logs",
                arguments = new { doorId = createdDoor!.Id, limit = 10 }
            })
        };
        var logsRes = await _client.PostAsJsonAsync("/mcp/messages", logsCall);
        Assert.Equal(HttpStatusCode.OK, logsRes.StatusCode);
        var logsRpcRes = await logsRes.Content.ReadFromJsonAsync<McpRpcResponse>();
        Assert.NotNull(logsRpcRes?.Result);
        var logsText = JsonSerializer.SerializeToElement(logsRpcRes!.Result).GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Contains("MCP Assistant", logsText);
    }

    [Fact]
    public async Task AccessLogs_StreamEndpoint_ReceivesStreamedEvent()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/logs/stream");
        request.Headers.Add("Accept", "text/event-stream");

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);
        var firstLine = await reader.ReadLineAsync(cts.Token);
        Assert.Equal(": connected", firstLine);

        // Read the empty separator line after : connected
        var emptyLine = await reader.ReadLineAsync(cts.Token);
        Assert.Equal("", emptyLine);

        // Use a separate client instance so the streaming request doesn't multiplex/block on in-memory TestServer handler
        using var client2 = _factory.CreateClient();

        // Create a test door
        var doorRes = await client2.PostAsJsonAsync("/api/doors", new AccessPoint
        {
            Name = "SSE Stream Door",
            LockProviderType = "GenericMqtt"
        }, cts.Token);
        var door = await doorRes.Content.ReadFromJsonAsync<AccessPoint>(cts.Token);
        Assert.NotNull(door);

        // Perform unlock which broadcasts to the event stream
        var unlockRes = await client2.PostAsync($"/api/doors/{door!.Id}/unlock", null, cts.Token);
        Assert.Equal(HttpStatusCode.OK, unlockRes.StatusCode);

        // Read the streamed event: "data: {json}"
        var dataLine = await reader.ReadLineAsync(cts.Token);
        Assert.NotNull(dataLine);
        Assert.StartsWith("data: ", dataLine!);

        var json = dataLine!.Substring("data: ".Length);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(door.Id, doc.RootElement.GetProperty("accessPointId").GetString());
        Assert.Equal("Unlocked", doc.RootElement.GetProperty("eventType").GetString());
    }
}

