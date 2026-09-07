# Intelligent Door Setup Wizard & Smart MQTT Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the AccessControl door setup experience from confusing transport data-vomit into an intuitive, progressive disclosure wizard with friendly-named contact sensors, zero MQTT leak in direct Z-Wave mode, and a real-time "Listen for Activity" physical sniffer.

**Architecture:** 
- Backend `MqttTopicDiscoveryService` strictly classifies binary contact sensors, parses friendly names and models from Zigbee2MQTT / Home Assistant announcements, and buffers recent state transitions for a live sniffer endpoint (`GET /api/discovery/sniff`).
- Frontend `DoorSetupWizard.tsx` adopts clean segmented hardware cards: selecting Direct Z-Wave JS shows detected node cards and unmounts all MQTT fields; selecting an integrated lock auto-configures Built-In Keypad inheritance; contact sensors are chosen via a friendly searchable catalog or live sniffer tap.
- Comprehensive end-to-end tests verify metric rejection, closed-loop sniffer events, SQLite persistence, and Playwright UI flow execution against the live container.

**Tech Stack:** .NET 10 (`net10.0`), C# 13, ASP.NET Core Minimal APIs/Controllers, SQLite, React 19, TypeScript strict mode, Zustand, Vitest, Playwright.

## Global Constraints
- Target framework: `net10.0`, C# 13, `<Nullable>enable</Nullable>`.
- Never leak MQTT topic fields or provider types when `Direct Z-Wave JS` is selected.
- Strict sensor filtering: reject any metric matching `battery`, `temp`, `humidity`, `linkquality`, `power`, `voltage`, `illuminance`, `energy`, `update`, `tamper`, `rssi`.
- Zero ESLint warnings (`eslint . --max-warnings 0`).
- Solution must compile cleanly with 0 warnings (`dotnet build AccessControl.slnx`).
- Commit each task atomically using `./commit.sh`.

---

### Task 1: Backend Discovery Engine Upgrade & Sniffer Service

**Files:**
- Modify: `src/AccessControl.Engine/Services/MqttTopicDiscoveryService.cs`
- Modify: `src/AccessControl.Web/Controllers/DiscoveryController.cs`
- Test: `tests/AccessControl.Tests.Harness/Services/MqttTopicDiscoveryTests.cs`

**Interfaces:**
- Consumes: MQTT topic and payload strings via `IMqttDiscoveryService.RecordTopic(string topic, string? payload)`.
- Produces: 
  - `IReadOnlyList<DiscoveredContactSensor> GetDiscoveredContactSensors()`
  - `SnifferResult SniffActivity(DateTimeOffset since)`
  - `GET /api/discovery/sensors` returning `DiscoveredContactSensor[]`
  - `GET /api/discovery/sniff?since={timestamp}` returning `SnifferResult`

- [x] **Step 1: Write the failing tests for strict metric rejection and sniffer buffer**

Create `tests/AccessControl.Tests.Harness/Services/MqttTopicDiscoveryTests.cs`:
```csharp
using AccessControl.Engine.Services;
using FluentAssertions;
using Xunit;

namespace AccessControl.Tests.Harness.Services;

public class MqttTopicDiscoveryTests
{
    [Fact]
    public void RecordTopic_RejectsNonContactMetrics_AndDiscoversContactSensors()
    {
        var svc = new MqttTopicDiscoveryService();

        // Feed realistic mixed homelab traffic
        svc.RecordTopic("zigbee2mqtt/front_door/battery", "{\"battery\":95}");
        svc.RecordTopic("zigbee2mqtt/front_door/linkquality", "{\"linkquality\":110}");
        svc.RecordTopic("zigbee2mqtt/front_door/temperature", "{\"temperature\":21.5}");
        svc.RecordTopic("homeassistant/sensor/washer_power/state", "1200");
        svc.RecordTopic("zigbee2mqtt/front_door_contact", "{\"contact\":true,\"battery\":95}");
        svc.RecordTopic("ring/keypad_v2/alarm/command", "{\"command\":\"disarm\"}");

        var sensors = svc.GetDiscoveredContactSensors();

        sensors.Should().ContainSingle(s => s.Topic == "zigbee2mqtt/front_door_contact");
        sensors.Should().NotContain(s => s.Topic.Contains("battery"));
        sensors.Should().NotContain(s => s.Topic.Contains("linkquality"));
        sensors.Should().NotContain(s => s.Topic.Contains("temperature"));
        sensors.Should().NotContain(s => s.Topic.Contains("power"));
    }

    [Fact]
    public void SniffActivity_DetectsRecentStateChanges_AfterGivenTimestamp()
    {
        var svc = new MqttTopicDiscoveryService();
        var t0 = DateTimeOffset.UtcNow;

        var initial = svc.SniffActivity(t0);
        initial.Detected.Should().BeFalse();

        // Simulate user physically opening the door
        svc.RecordTopic("zigbee2mqtt/patio_door_contact", "{\"contact\":false}");

        var detected = svc.SniffActivity(t0);
        detected.Detected.Should().BeTrue();
        detected.Event.Should().NotBeNull();
        detected.Event!.Topic.Should().Be("zigbee2mqtt/patio_door_contact");
        detected.Event!.State.Should().Be("OPEN");
    }
}
```

- [x] **Step 2: Run test to verify failure**

Run: `dotnet test tests/AccessControl.Tests.Harness/AccessControl.Tests.Harness.csproj --filter "MqttTopicDiscoveryTests"`
Expected: FAIL with compilation error (methods `GetDiscoveredContactSensors` and `SniffActivity` do not exist).

- [x] **Step 3: Implement strict filtering, friendly name resolution, and sniffer in `MqttTopicDiscoveryService.cs`**

Modify `src/AccessControl.Engine/Services/MqttTopicDiscoveryService.cs`:
Add data contracts:
```csharp
public record DiscoveredContactSensor(
    string Topic,
    string DeviceName,
    string Integration,
    string Model,
    string CurrentState,
    DateTimeOffset LastSeen);

public record SnifferEvent(
    string Topic,
    string DeviceName,
    string Model,
    string State,
    DateTimeOffset Timestamp);

public record SnifferResult(
    bool Detected,
    SnifferEvent? Event = null);
```

Add to `IMqttDiscoveryService`:
```csharp
IReadOnlyList<DiscoveredContactSensor> GetDiscoveredContactSensors();
SnifferResult SniffActivity(DateTimeOffset since);
```

Implement in `MqttTopicDiscoveryService`:
- Blacklist metric strings: `new[] { "battery", "linkquality", "temperature", "temp", "humidity", "illuminance", "power", "voltage", "energy", "action", "update", "tamper", "rssi", "motion", "occupancy" }`.
- Parse contact signatures: check if topic contains `contact` or payload has `"contact":`.
- Maintain a thread-safe `ConcurrentQueue<SnifferEvent>` with a max capacity of 50 items.
- Expose `GetDiscoveredContactSensors()` and `SniffActivity(DateTimeOffset since)`.

- [x] **Step 4: Expose endpoints in `DiscoveryController.cs`**

Modify `src/AccessControl.Web/Controllers/DiscoveryController.cs`:
```csharp
[HttpGet("sensors")]
public IActionResult GetDiscoveredContactSensors()
{
    var sensors = _discoveryService.GetDiscoveredContactSensors();
    return Ok(sensors);
}

[HttpGet("sniff")]
public IActionResult SniffActivity([FromQuery] DateTimeOffset? since)
{
    var referenceTime = since ?? DateTimeOffset.UtcNow.AddSeconds(-15);
    var result = _discoveryService.SniffActivity(referenceTime);
    return Ok(result);
}
```

- [x] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/AccessControl.Tests.Harness/AccessControl.Tests.Harness.csproj --filter "MqttTopicDiscoveryTests"`
Expected: PASS (both tests green).

- [x] **Step 6: Commit**

Run:
```bash
./commit.sh "feat(discovery): add strict contact sensor filtering and live activity sniffer"
```

---

### Task 2: Frontend Types & API Client Updates

**Files:**
- Modify: `src/AccessControl.UI/src/types/index.ts`
- Modify: `src/AccessControl.UI/src/api/apiClient.ts`
- Modify: `src/AccessControl.UI/src/api/apiClient.test.ts`

**Interfaces:**
- Consumes: `/api/discovery/sensors` and `/api/discovery/sniff` endpoints from Task 1.
- Produces:
  - `DiscoveredContactSensor` and `SnifferResult` types in `types/index.ts`.
  - `apiClient.discovery.getSensors(): Promise<DiscoveredContactSensor[]>`
  - `apiClient.discovery.sniff(since: string): Promise<SnifferResult>`

- [x] **Step 1: Write failing test in `apiClient.test.ts`**

In `src/AccessControl.UI/src/api/apiClient.test.ts`:
```typescript
it('discovery.getSensors fetches strictly filtered contact sensors', async () => {
  const mockSensors: DiscoveredContactSensor[] = [
    {
      topic: 'zigbee2mqtt/front_door_contact',
      deviceName: 'Front Door Contact',
      integration: 'Zigbee2MQTT',
      model: 'Aqara MCCGQ11LM',
      currentState: 'closed',
      lastSeen: '2026-09-07T12:00:00Z',
    },
  ];
  mockFetch.mockResolvedValueOnce({
    ok: true,
    json: async () => mockSensors,
  });

  const result = await apiClient.discovery.getSensors();
  expect(result).toEqual(mockSensors);
  expect(mockFetch).toHaveBeenCalledWith(expect.stringContaining('/api/discovery/sensors'), expect.any(Object));
});

it('discovery.sniff calls sniff endpoint with since timestamp', async () => {
  const mockSnifferResult: SnifferResult = {
    detected: true,
    event: {
      topic: 'zigbee2mqtt/front_door_contact',
      deviceName: 'Front Door Contact',
      model: 'Aqara MCCGQ11LM',
      state: 'OPEN',
      timestamp: '2026-09-07T12:05:00Z',
    },
  };
  mockFetch.mockResolvedValueOnce({
    ok: true,
    json: async () => mockSnifferResult,
  });

  const result = await apiClient.discovery.sniff('2026-09-07T12:04:50Z');
  expect(result.detected).toBe(true);
  expect(result.event?.state).toBe('OPEN');
});
```

- [x] **Step 2: Run test to verify failure**

Run: `cd src/AccessControl.UI && npm test -- src/api/apiClient.test.ts`
Expected: FAIL (`getSensors` and `sniff` are not functions).

- [x] **Step 3: Update `types/index.ts` and `apiClient.ts`**

In `src/AccessControl.UI/src/types/index.ts`, add:
```typescript
export interface DiscoveredContactSensor {
  topic: string;
  deviceName: string;
  integration: string;
  model: string;
  currentState: string;
  lastSeen: string;
}

export interface SnifferEvent {
  topic: string;
  deviceName: string;
  model: string;
  state: string;
  timestamp: string;
}

export interface SnifferResult {
  detected: boolean;
  event?: SnifferEvent;
}
```

In `src/AccessControl.UI/src/api/apiClient.ts`, add to `discovery`:
```typescript
  discovery: {
    getTopics: async (): Promise<DiscoveredTopic[]> => {
      return request<DiscoveredTopic[]>('/api/discovery');
    },
    getSensors: async (): Promise<DiscoveredContactSensor[]> => {
      return request<DiscoveredContactSensor[]>('/api/discovery/sensors');
    },
    sniff: async (sinceIso: string): Promise<SnifferResult> => {
      return request<SnifferResult>(`/api/discovery/sniff?since=${encodeURIComponent(sinceIso)}`);
    },
  },
```

- [x] **Step 4: Run test to verify pass**

Run: `cd src/AccessControl.UI && npm test -- src/api/apiClient.test.ts`
Expected: PASS.

- [x] **Step 5: Commit**

Run:
```bash
./commit.sh "feat(ui): add contact sensor discovery and sniffer api contracts"
```

---

### Task 3: Redesign DoorSetupWizard (Progressive Disclosure & Smart Sensor Picker)

**Files:**
- Modify: `src/AccessControl.UI/src/components/doors/DoorSetupWizard.tsx`
- Modify: `src/AccessControl.UI/src/components/doors/DoorSetupWizard.test.tsx`

**Interfaces:**
- Consumes:
  - `useSettingsStore` (detected Z-Wave nodes, transport type).
  - `apiClient.discovery.getSensors()` and `apiClient.discovery.sniff()`.
- Produces:
  - Form state with zero MQTT fields visible in `Direct Z-Wave JS` mode.
  - Context-aware Built-In Keypad inheritance.
  - Searchable contact sensor dropdown with live sniffer bar.
  - Submits valid `Partial<AccessPoint>` payload on save.

- [x] **Step 1: Write failing tests in `DoorSetupWizard.test.tsx`**

Test:
1. `Direct Z-Wave JS mode renders node cards and hides all MQTT input fields`:
   - Asserts node 39 card exists.
   - Asserts `input[placeholder*="zwave/"]` does NOT exist in the DOM.
2. `Selecting integrated lock auto-selects Built-In Keypad`:
   - Clicking Node 39 selects Built-In Keypad and indicates PIN slots sync directly.
3. `Contact Sensor Card renders friendly dropdown and does not render button swarms`:
   - Asserts `<select>` has option `Front Door Contact — Aqara MCCGQ11LM`.
   - Asserts no buttons with raw topics exist.
4. `Clicking Listen for Activity enters listening state and updates selection when event detected`.

- [x] **Step 2: Run test to verify failure**

Run: `cd src/AccessControl.UI && npm test -- src/components/doors/DoorSetupWizard.test.tsx`
Expected: FAIL.

- [x] **Step 3: Implement progressive disclosure UI in `DoorSetupWizard.tsx`**

1. Replace provider mixing with segmented control:
   - `lockHardwareType`: `'DirectZWave' | 'MqttLock'`.
   - If `'DirectZWave'`:
     - Render discovered lock nodes as interactive cards.
     - Never render MQTT topic input.
   - If `'MqttLock'`:
     - Render MQTT provider dropdown and topic text input.
2. Keypad card:
   - If `lockHardwareType === 'DirectZWave'` and lock selected has built-in keypad (e.g. BE469ZP, Yale, August):
     - Default to `'BuiltInKeypad'`.
     - Disable/omit redundant topic inputs.
3. Door Contact Sensor card:
   - Segmented toggle: `'None' | 'ZWaveNode' | 'MqttContact'`.
   - In `'MqttContact'`:
     - Searchable select element loaded from `apiClient.discovery.getSensors()`.
     - Remove the flex-wrap button swarm completely.
     - Live sniffer button: polls `apiClient.discovery.sniff(startTime)` every 1.5s for 15s. On match, updates selected topic and stops polling.
4. Clean auto-lock drawer:
   - Sliders for Day (seconds) and Night (seconds).

- [x] **Step 4: Run tests to verify pass & zero lint warnings**

Run: `cd src/AccessControl.UI && npm test -- src/components/doors/DoorSetupWizard.test.tsx && npm run lint`
Expected: PASS and 0 warnings.

- [x] **Step 5: Build UI bundle into wwwroot**

Run: `cd src/AccessControl.UI && npm run build`
Expected: Succeeded.

- [x] **Step 6: Commit**

Run:
```bash
./commit.sh "feat(ui): redesign door setup wizard with progressive disclosure and live sniffer"
```

---

### Task 4: Closed-Loop Integration Tests & Playwright Flow Harness

**Files:**
- Modify: `tests/AccessControl.Tests.Harness/Services/MqttTopicDiscoveryTests.cs`
- Modify: `src/AccessControl.UI/harness/specs/flows.spec.ts`

**Interfaces:**
- Consumes: Running system at `http://127.0.0.1:8150` or mock server.
- Produces:
  - Verified backend integration test verifying real SQLite door persistence with progressive disclosure payload.
  - Updated Playwright Flow 3 executing the new wizard layout, selecting Node 39, verifying zero MQTT fields, selecting a friendly contact sensor, and publishing screenshot to the Agent Preview Hub.

- [x] **Step 1: Add SQLite persistence test in `AccessControl.Tests.Harness`**

Create `tests/AccessControl.Tests.Harness/Api/AccessPointCreationTests.cs`:
- Simulates creating a door via `POST /api/access-points` with:
  - `name: "Front Entryway"`
  - `lockProviderType: "ZWaveWebSocket"`, `lockConfigJson: "{\"nodeId\":39}"`
  - `keypadProviderType: "BuiltInKeypad"`, `keypadConfigJson: "{\"nodeId\":39}"`
  - `doorSensorProviderType: "AqaraZigbee"`, `doorSensorConfigJson: "{\"topic\":\"zigbee2mqtt/front_door_contact\"}"`
- Queries SQLite directly via Dapper.
- Asserts door record exists with correct properties, auto-lock settings, and foreign keys.

- [x] **Step 2: Run backend integration tests**

Run: `dotnet test tests/AccessControl.Tests.Harness/AccessControl.Tests.Harness.csproj`
Expected: PASS.

- [x] **Step 3: Update Playwright Flow 3 in `harness/specs/flows.spec.ts`**

Update Flow 3:
- Opens Add Door modal.
- Verifies `Direct Z-Wave JS` mode is active.
- Selects `Node 39` card.
- Asserts `input[placeholder*="zwave/"]` does NOT exist in the DOM (verifies zero MQTT leak).
- Asserts `Built-In Lock Keypad` is active.
- Verifies contact sensor selector contains friendly options.
- Captures screenshot: `harness/output/03-door-setup-wizard.png`.
- Closes dialog.

- [x] **Step 4: Run full flow harness and publish to Agent Preview Hub**

Run: `cd src/AccessControl.UI && npm run harness`
Expected: All 5 flows pass, screenshot updated, report published to `https://preview.wileyriley.com/accesscontrol-harness/`.

- [x] **Step 5: Commit & push**

Run:
```bash
./commit.sh "test(e2e): verify door setup progressive disclosure and publish harness report"
git push origin develop
```
