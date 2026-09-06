# CodeMaster Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build CodeMaster, a high-performance .NET 10 and React 19 containerized access control and lock/keypad management platform that replaces Keymaster with zero Home Assistant entity bloat.

**Architecture:** A single Docker container hosting an ASP.NET Core `net10.0` backend with embedded SQLite WAL and Dapper, a decoupled `Channel<MqttInboundMessage>` MQTT ingestion engine, pluggable lock/keypad/sensor providers, Home Assistant MQTT Discovery + Apprise notifications, an embedded Model Context Protocol (MCP) server, and a compiled React 19 + TypeScript + Zustand SPA supporting standalone use and Home Assistant Ingress.

**Tech Stack:** .NET 10 (`net10.0`), C# 13, Dapper, SQLite (WAL mode), MQTTnet 4.x, React 19, TypeScript 5.x, Zustand, Vite, Playwright Layout Inspector, Docker, GitHub Actions (CI + GHCR Package Registry).

## Global Constraints

- Target .NET 10 (`net10.0`) with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` across all C# projects.
- Solution format must use modern `.slnx` (`codemaster.slnx`).
- SQLite pragmas must be explicitly enforced: `PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;`.
- Use Dapper with dedicated parameterized SQL scripts (no heavy ORMs, no raw inline SQL strings in business logic).
- TypeScript in strict mode (`"strict": true`) with zero linter warnings (`eslint . --max-warnings 0`).
- Strict 4-point Playwright layout audit: `toHaveNoLayoutOverflow`, `toHaveMobileFit`, `toHaveTouchFriendlyTargets({ minSize: 24 })`, `toPassLayoutAudit({ minScore: 85 })`.
- All C# public services must implement narrow client-focused interfaces (`I*`) for 100% testability.
- Every async method must accept and propagate a `CancellationToken`.
- CI/CD must implement the 4-stage quality gate blueprint and publish container images to GitHub Container Registry (GHCR).

---

### Task 1: Scaffolding & Toolbelt Baseline

**Files:**
- Create: `codemaster.slnx`
- Create: `Directory.Build.props`
- Create: `.gitignore`
- Create: `commit.sh`
- Create: `verify_release.py`
- Create: `ARCHITECTURE.md`

**Interfaces:**
- Produces: Base build solution layout, shared build parameters, versioning script.

- [ ] **Step 1: Write Directory.Build.props and .gitignore**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest</AnalysisLevel>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create .slnx solution and initial project directories**

Run:
```bash
dotnet new sln -n codemaster --format slnx
mkdir -p src/CodeMaster.Core src/CodeMaster.Data src/CodeMaster.Engine src/CodeMaster.Mcp src/CodeMaster.Web src/CodeMaster.UI
mkdir -p tests/CodeMaster.Tests.Unit tests/CodeMaster.Tests.Harness tests/CodeMaster.Tests.Simulator tests/CodeMaster.UI.Tests
```

- [ ] **Step 3: Add commit.sh and verify_release.py from AgenticEngineeringToolbelt**

Copy and adapt scripts from `/containers/dev/AgenticEngineeringToolbelt/templates/scripts/` to enforce SemVer and atomic git commits.

- [ ] **Step 4: Create living ARCHITECTURE.md with top-down Mermaid topology**

Document the component layout, subsystem responsibilities, and ports.

- [ ] **Step 5: Verify build infrastructure & Commit**

Run: `dotnet build codemaster.slnx` (empty or base check).  
Commit: `git commit -m "chore: scaffold solution, toolbelt scripts, and build props"`

---

### Task 2: Core Domain Model & Provider Interfaces (`CodeMaster.Core`)

**Files:**
- Create: `src/CodeMaster.Core/CodeMaster.Core.csproj`
- Create: `src/CodeMaster.Core/Models/User.cs`
- Create: `src/CodeMaster.Core/Models/Credential.cs`
- Create: `src/CodeMaster.Core/Models/AccessPoint.cs`
- Create: `src/CodeMaster.Core/Models/AccessPolicy.cs`
- Create: `src/CodeMaster.Core/Models/HardwareSlot.cs`
- Create: `src/CodeMaster.Core/Models/AccessLog.cs`
- Create: `src/CodeMaster.Core/Interfaces/ILockProvider.cs`
- Create: `src/CodeMaster.Core/Interfaces/IKeypadProvider.cs`
- Create: `src/CodeMaster.Core/Interfaces/IDoorSensorProvider.cs`
- Create: `src/CodeMaster.Core/Interfaces/INotificationDispatcher.cs`
- Test: `tests/CodeMaster.Tests.Unit/CoreModelsTests.cs`

**Interfaces:**
- Produces: Core domain entities, capability enums (`LockCapabilities`, `KeypadCapabilities`), and provider contracts.

- [ ] **Step 1: Write failing unit test verifying domain model validation**

```csharp
[Fact]
public void AccessPolicy_IsActiveAt_ReturnsTrue_WithinWindow()
{
    var policy = new AccessPolicy {
        ScheduleType = ScheduleType.WeeklyRecurring,
        DaysOfWeek = 127, // All days
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(17, 0),
        IsEnabled = true
    };
    var testTime = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc); // Monday 12:00
    Assert.True(policy.IsActiveAt(testTime));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: Compilation failure (types do not exist yet).

- [ ] **Step 3: Implement domain models and interfaces**

Implement `User`, `Credential`, `AccessPoint`, `AccessPolicy` with schedule evaluation logic, `HardwareSlot`, and provider interfaces (`ILockProvider`, `IKeypadProvider`, `IDoorSensorProvider`, `INotificationDispatcher`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: PASS.

- [ ] **Step 5: Commit**

`git commit -m "feat(core): implement domain entities and provider abstractions"`

---

### Task 3: Relational Persistence Layer (`CodeMaster.Data`)

**Files:**
- Create: `src/CodeMaster.Data/CodeMaster.Data.csproj`
- Create: `src/CodeMaster.Data/Db/IDbConnectionFactory.cs`
- Create: `src/CodeMaster.Data/Db/SqliteConnectionFactory.cs`
- Create: `src/CodeMaster.Data/Db/DatabaseSeederService.cs`
- Create: `src/CodeMaster.Data/Repositories/UserRepository.cs`
- Create: `src/CodeMaster.Data/Repositories/AccessPointRepository.cs`
- Create: `src/CodeMaster.Data/Repositories/AuditLogRepository.cs`
- Create: `src/CodeMaster.Data/Scripts/InitSchema.sql`
- Test: `tests/CodeMaster.Tests.Unit/DatabaseSeederTests.cs`

**Interfaces:**
- Consumes: Core models from `CodeMaster.Core`.
- Produces: `IDbConnectionFactory`, repositories, idempotent schema migration.

- [ ] **Step 1: Write failing test for DatabaseSeederService**

```csharp
[Fact]
public async Task Seeder_InitializesDatabase_TablesExist()
{
    using var factory = new SqliteTestConnectionFactory();
    var seeder = new DatabaseSeederService(factory, NullLogger<DatabaseSeederService>.Instance);
    await seeder.InitializeAsync(CancellationToken.None);
    using var conn = factory.CreateConnection();
    var tables = await conn.QueryAsync<string>("SELECT name FROM sqlite_master WHERE type='table';");
    Assert.Contains("AccessPoints", tables);
    Assert.Contains("Credentials", tables);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: FAIL.

- [ ] **Step 3: Implement SQLite connection factory, WAL pragmas, and Dapper repositories**

Enforce `PRAGMA journal_mode = WAL;`, execute `InitSchema.sql`, and write Dapper repositories with parameterized queries.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: PASS.

- [ ] **Step 5: Commit**

`git commit -m "feat(data): implement SQLite WAL connection factory and Dapper repositories"`

---

### Task 4: MQTT Ingestion & Inbound Channel (`CodeMaster.Engine`)

**Files:**
- Create: `src/CodeMaster.Engine/CodeMaster.Engine.csproj`
- Create: `src/CodeMaster.Engine/Channels/MqttInboundChannel.cs`
- Create: `src/CodeMaster.Engine/Mqtt/MqttClientService.cs`
- Create: `src/CodeMaster.Engine/Mqtt/MqttOptions.cs`
- Test: `tests/CodeMaster.Tests.Unit/MqttInboundChannelTests.cs`

**Interfaces:**
- Produces: `IMqttInboundChannel`, `IMqttClientService` for subscribing and publishing.

- [ ] **Step 1: Write failing test for bounded Channel<MqttInboundMessage>**

```csharp
[Fact]
public async Task Channel_PublishesAndConsumesMessages_InOrder()
{
    var channel = new MqttInboundChannel(capacity: 100);
    var msg = new MqttInboundMessage("zwave/front_door", "{\"state\":\"locked\"}");
    await channel.Writer.WriteAsync(msg);
    var read = await channel.Reader.ReadAsync();
    Assert.Equal("zwave/front_door", read.Topic);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: FAIL.

- [ ] **Step 3: Implement MqttInboundChannel and MQTTnet client service**

Wire up `Channel.CreateBounded<MqttInboundMessage>` with `FullMode = BoundedChannelFullMode.Wait`, resilient auto-reconnect, and cancellation token propagation.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: PASS.

- [ ] **Step 5: Commit**

`git commit -m "feat(engine): implement MQTTnet client service and inbound channel pipeline"`

---

### Task 5: Hardware & Keypad Providers (`CodeMaster.Engine/Providers`)

**Files:**
- Create: `src/CodeMaster.Engine/Providers/Locks/ZWaveJsMqttLockProvider.cs`
- Create: `src/CodeMaster.Engine/Providers/Locks/GenericMqttLockProvider.cs`
- Create: `src/CodeMaster.Engine/Providers/Keypads/RingMqttKeypadProvider.cs`
- Create: `src/CodeMaster.Engine/Providers/Keypads/BuiltInLockKeypadProvider.cs`
- Create: `src/CodeMaster.Engine/Providers/Sensors/MqttContactSensorProvider.cs`
- Test: `tests/CodeMaster.Tests.Unit/ProviderParsingTests.cs`

**Interfaces:**
- Consumes: `ILockProvider`, `IKeypadProvider`, `IDoorSensorProvider` from Core.
- Produces: Concrete providers parsing Ring Keypad disarm events, Z-Wave UserCode CC messages, and door contact payloads.

- [ ] **Step 1: Write failing test for RingMqttKeypadProvider parsing**

```csharp
[Fact]
public void RingKeypad_ParsesDisarmCommandWithPin()
{
    var provider = new RingMqttKeypadProvider();
    var payload = "{\"command\":\"disarm\",\"code\":\"4821\"}";
    var msg = new MqttInboundMessage("ring/loc/alarm/command", payload);
    var success = provider.TryParseKeypadEvent(msg, out var evt);
    Assert.True(success);
    Assert.Equal("4821", evt!.Pin);
    Assert.Equal(KeypadAction.Disarm, evt.Action);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: FAIL.

- [ ] **Step 3: Implement provider parsing and command generation**

Implement parsing for `ring-mqtt`, Z-Wave JS UI topic hierarchies, and generic MQTT contact sensors with configurable open/closed strings.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: PASS.

- [ ] **Step 5: Commit**

`git commit -m "feat(providers): implement Z-Wave, Ring Keypad, and contact sensor providers"`

---

### Task 6: Access Policy Evaluator, Hardware Sync & Auto-Lock Engine

**Files:**
- Create: `src/CodeMaster.Engine/Services/AccessPolicyEvaluator.cs`
- Create: `src/CodeMaster.Engine/Services/HardwareSlotSyncWorker.cs`
- Create: `src/CodeMaster.Engine/Services/AutoLockStateMachine.cs`
- Test: `tests/CodeMaster.Tests.Unit/AutoLockStateMachineTests.cs`
- Test: `tests/CodeMaster.Tests.Unit/AccessPolicyEvaluatorTests.cs`

**Interfaces:**
- Consumes: Providers, Repositories, Inbound Channel.
- Produces: Automated PIN verification, slot synchronization loop, intelligent door-aware auto-lock timer.

- [ ] **Step 1: Write failing test for AutoLockStateMachine door interruption**

```csharp
[Fact]
public void AutoLock_SuspendsWhenDoorIsOpen_ResumesWhenClosed()
{
    var sm = new AutoLockStateMachine(timeoutSeconds: 60);
    sm.OnLockStateChanged(LockState.Unlocked);
    sm.OnDoorContactChanged(DoorContactState.Open);
    Assert.Equal(AutoLockStatus.PausedDoorOpen, sm.Status);
    sm.OnDoorContactChanged(DoorContactState.Closed);
    Assert.Equal(AutoLockStatus.CountingDown, sm.Status);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: FAIL.

- [ ] **Step 3: Implement AutoLockStateMachine, HardwareSlotSyncWorker, and AccessPolicyEvaluator**

Implement state machine, 15-second jam detection retry, hardware slot sync reconciliation, and policy evaluation with salted hash comparison.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: PASS.

- [ ] **Step 5: Commit**

`git commit -m "feat(engine): implement policy evaluator, slot sync worker, and auto-lock state machine"`

---

### Task 7: Home Assistant MQTT Discovery & Notification Dispatcher

**Files:**
- Create: `src/CodeMaster.Engine/Services/HomeAssistantDiscoveryService.cs`
- Create: `src/CodeMaster.Engine/Services/AppriseNotificationDispatcher.cs`
- Test: `tests/CodeMaster.Tests.Unit/HaDiscoveryPayloadTests.cs`

**Interfaces:**
- Produces: Standard HA MQTT Discovery config payloads for `event.<door>_access`, `lock.<door>`, `binary_sensor.<door>_sensor`, `switch.<door>_autolock`, and Apprise webhooks.

- [ ] **Step 1: Write failing test for HA MQTT Discovery JSON schema**

```csharp
[Fact]
public void HaDiscovery_GeneratesValidEventEntityPayload()
{
    var svc = new HomeAssistantDiscoveryService();
    var door = new AccessPoint { Id = "door_1", Name = "Side Door" };
    var payload = svc.BuildEventDiscoveryPayload(door);
    Assert.Contains("\"event_types\":[\"keypad_unlock\",\"manual_unlock\",\"auto_lock\"]", payload);
    Assert.Contains("homeassistant/event/codemaster_door_1/config", payload);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: FAIL.

- [ ] **Step 3: Implement HomeAssistantDiscoveryService and AppriseNotificationDispatcher**

Construct MQTT discovery JSON strings compliant with Home Assistant core MQTT event specifications and HTTP client for Apprise alerts.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: PASS.

- [ ] **Step 5: Commit**

`git commit -m "feat(notify): implement HA MQTT Discovery and Apprise notification dispatchers"`

---

### Task 8: Web API Controllers & Model Context Protocol (MCP) Server

**Files:**
- Create: `src/CodeMaster.Web/Controllers/AccessPointsController.cs`
- Create: `src/CodeMaster.Web/Controllers/UsersController.cs`
- Create: `src/CodeMaster.Web/Controllers/AccessLogsController.cs`
- Create: `src/CodeMaster.Web/Controllers/DiscoveryController.cs`
- Create: `src/CodeMaster.Web/Program.cs`
- Create: `src/CodeMaster.Mcp/CodeMasterMcpServer.cs`
- Create: `src/CodeMaster.Mcp/Tools/DoorTools.cs`
- Test: `tests/CodeMaster.Tests.Unit/ApiControllerTests.cs`

**Interfaces:**
- Produces: REST endpoints (`/api/doors`, `/api/users`, `/api/logs`, `/api/discovery`), `/health` probe, and MCP SSE endpoint (`/mcp/sse`).

- [ ] **Step 1: Write failing test for AccessPointsController and /health probe**

```csharp
[Fact]
public async Task HealthProbe_ReturnsHealthyStatus()
{
    await using var app = new WebApplicationFactory<Program>();
    using var client = app.CreateClient();
    var response = await client.GetAsync("/health");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: FAIL.

- [ ] **Step 3: Implement controllers, /health endpoint, and MCP server tools**

Wire up `Program.cs` with controllers, forward-auth header support (`Remote-User`), static file fallback, and MCP tools (`codemaster__list_doors`, `codemaster__unlock_door`, `codemaster__create_guest_pin`, `codemaster__get_access_logs`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CodeMaster.Tests.Unit`  
Expected: PASS.

- [ ] **Step 5: Commit**

`git commit -m "feat(web): implement REST controllers, health probe, and MCP server"`

---

### Task 9: React 19 Frontend Dashboard (`CodeMaster.UI`)

**Files:**
- Create: `src/CodeMaster.UI/package.json`
- Create: `src/CodeMaster.UI/vite.config.ts`
- Create: `src/CodeMaster.UI/tsconfig.json`
- Create: `src/CodeMaster.UI/src/styles/theme.css`
- Create: `src/CodeMaster.UI/src/api/apiClient.ts`
- Create: `src/CodeMaster.UI/src/stores/useDoorStore.ts`
- Create: `src/CodeMaster.UI/src/stores/useUserStore.ts`
- Create: `src/CodeMaster.UI/src/stores/useAuditStore.ts`
- Create: `src/CodeMaster.UI/src/components/doors/DoorCard.tsx`
- Create: `src/CodeMaster.UI/src/components/doors/DoorSetupWizard.tsx`
- Create: `src/CodeMaster.UI/src/components/users/ScheduleEditor.tsx`
- Create: `src/CodeMaster.UI/src/components/logs/LiveEventFeed.tsx`
- Create: `src/CodeMaster.UI/src/App.tsx`
- Test: `tests/CodeMaster.UI.Tests/LayoutAudit.spec.ts`

**Interfaces:**
- Consumes: REST API and dynamic `X-Ingress-Path` base URL.
- Produces: Compiled responsive SPA bundle in `src/CodeMaster.Web/wwwroot/`.

- [ ] **Step 1: Initialize Vite React 19 + TypeScript project**

```bash
cd src/CodeMaster.UI
npm init -y
npm install react@19 react-dom@19 zustand lucide-react
npm install -D typescript @types/react @types/react-dom vite @vitejs/plugin-react
```

- [ ] **Step 2: Implement apiClient with Ingress path resolution**

Configure `apiClient.ts` to inspect `<meta name="base-path">` or `window.__CODEMASTER_BASE_PATH__` for Home Assistant Ingress compatibility.

- [ ] **Step 3: Implement Zustand stores and bespoke UI components**

Implement `DoorCard`, `DoorSetupWizard` (with auto-discovered MQTT topics), `ScheduleEditor` (day pills and sliders), and `LiveEventFeed`.

- [ ] **Step 4: Build UI bundle into wwwroot**

Run: `npm run build`  
Verify: `src/CodeMaster.Web/wwwroot/index.html` generated with relative asset links.

- [ ] **Step 5: Run Playwright Layout Inspector audit**

Execute 4-point layout audit: zero horizontal overflow, mobile fit, touch targets $\ge$ 24px, audit score $\ge$ 85.

- [ ] **Step 6: Commit**

`git commit -m "feat(ui): implement React 19 dashboard with Zustand stores and Ingress support"`

---

### Task 10: Synthetic Test Harness & Live Docker Testing Stack

**Files:**
- Create: `tests/CodeMaster.Tests.Harness/MockMqttBroker.cs`
- Create: `tests/CodeMaster.Tests.Simulator/Program.cs`
- Create: `tests/CodeMaster.Tests.Simulator/Dockerfile`
- Create: `Dockerfile` (Production multi-stage build)
- Create: `docker-compose.yaml` (Production deployment)
- Create: `docker-compose.test.yaml` (Live test stack)
- Test: `tests/CodeMaster.Tests.Harness/RingKeypadToLockClosedLoopTests.cs`

**Interfaces:**
- Produces: Full closed-loop simulation testing and live container integration stack.

- [ ] **Step 1: Implement MockMqttBroker and Ring Keypad Closed-Loop Test**

```csharp
[Fact]
public async Task RingKeypad_DisarmWithValidPin_IssuesAugustUnlockCommand()
{
    var harness = new ControlsTestHarness();
    await harness.SetupDoorAsync("door_august", lockTopic: "zwave/august", keypadTopic: "ring/kp");
    await harness.AddUserWithPinAsync("Steve", "4821", doorId: "door_august");

    // Inject simulated Ring Keypad PIN entry
    await harness.SimulateKeypadDisarmAsync("ring/kp", "4821");

    // Verify August lock command received on broker within 50ms
    var unlockMsg = await harness.WaitForPublishedMessageAsync("zwave/august/door_lock/endpoint_0/targetState/set", timeoutMs: 500);
    Assert.Equal("false", unlockMsg.Payload);
}
```

- [ ] **Step 2: Run closed-loop test to verify it passes**

Run: `dotnet test tests/CodeMaster.Tests.Harness`  
Expected: PASS.

- [ ] **Step 3: Create multi-stage Dockerfile and docker-compose.test.yaml**

```dockerfile
# Multi-stage Dockerfile: Node Vite build + .NET 10 SDK publish + runtime
FROM node:22-alpine AS ui-build
WORKDIR /app/ui
COPY src/CodeMaster.UI/package*.json ./
RUN npm ci
COPY src/CodeMaster.UI/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY Directory.Build.props codemaster.slnx ./
COPY src/ src/
COPY --from=ui-build /app/src/CodeMaster.Web/wwwroot src/CodeMaster.Web/wwwroot
RUN dotnet publish src/CodeMaster.Web/CodeMaster.Web.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /out ./
VOLUME ["/app/data"]
EXPOSE 8150
ENTRYPOINT ["dotnet", "CodeMaster.Web.dll"]
```

- [ ] **Step 4: Spin up docker-compose.test.yaml and verify fullstack smoke test**

Run:
```bash
docker compose -f docker-compose.test.yaml up -d --build
curl -f http://localhost:8155/health
docker compose -f docker-compose.test.yaml down
```
Expected: HTTP 200 OK.

- [ ] **Step 5: Commit**

`git commit -m "feat(testing): add synthetic harness, hardware simulator, and live Docker test stack"`

---

### Task 11: Home Assistant Add-on Packaging

**Files:**
- Create: `addon/config.yaml`
- Create: `addon/build.yaml`
- Create: `addon/DOCS.md`
- Create: `addon/CHANGELOG.md`

**Interfaces:**
- Produces: Hass.io add-on specification with Ingress support and complete documentation.

- [ ] **Step 1: Create addon/config.yaml**

```yaml
name: "CodeMaster"
description: "Universal Access Control & Lock/Keypad Synchronization Engine"
version: "1.0.0"
slug: "codemaster"
arch:
  - aarch64
  - amd64
init: false
ingress: true
ingress_port: 8150
panel_icon: "mdi:lock-smart"
options:
  mqtt_host: "core-mosquitto"
  mqtt_port: 1883
schema:
  mqtt_host: "str"
  mqtt_port: "int"
```

- [ ] **Step 2: Commit**

`git commit -m "feat(addon): package Home Assistant Add-on with Ingress"`

---

### Task 12: CI/CD Quality Gates & GitHub Container Registry (GHCR) Publishing

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/codeql.yml`
- Create: `.github/workflows/docker-publish.yml`
- Modify: `verify_release.py`

**Interfaces:**
- Consumes: `standards/CI_CD_PIPELINES.md` and templates from `AgenticEngineeringToolbelt`.
- Produces: 4-stage GitHub Actions CI gate (release audit, parallel backend/frontend tests, fullstack smoke probe) and multi-arch Docker image publication to `ghcr.io/${{ github.repository }}`.

- [ ] **Step 1: Implement .github/workflows/ci.yml (4-Stage Quality Gate Blueprint)**

Configure stages:
1. `release-verification`: runs `python3 verify_release.py --ci`.
2. `backend`: .NET 10 build, xUnit test execution, code coverage collection.
3. `frontend`: Node 22, strict ESLint (`--max-warnings 0`), Vite build, Vitest, Playwright layout tests.
4. `smoke`: background process spawn, `/health` probe loop, live handshake.

- [ ] **Step 2: Implement .github/workflows/codeql.yml**

Configure CodeQL static security analysis for `csharp` and `javascript-typescript`.

- [ ] **Step 3: Implement .github/workflows/docker-publish.yml**

Configure multi-platform (`linux/amd64`, `linux/arm64`) container builds via `docker/build-push-action@v5` publishing to `ghcr.io` with SemVer and `latest` tags.

- [ ] **Step 4: Run verify_release.py local verification**

Run: `python3 verify_release.py`  
Verify: SemVer synchronization and markdown link integrity pass cleanly.

- [ ] **Step 5: Commit**

`git commit -m "ci(github): add 4-stage quality gate workflows and GHCR container publishing"`

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-06-codemaster-implementation.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration.
**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
