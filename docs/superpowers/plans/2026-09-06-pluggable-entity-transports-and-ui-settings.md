# Pluggable Entity Transports & UI Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decouple hardware entity communications behind an extensible `ITransport` abstraction (supporting direct Z-Wave JS WebSocket and Mosquitto MQTT), persist settings in SQLite with container environment variable fallbacks, and build an interactive UI Settings manager.

**Architecture:** Core interfaces (`ITransport`, `ILockTransport`, `IKeypadTransport`, `ISensorTransport`, `ITransportRegistry`) decouple domain logic from wire protocols. `ZWaveWebSocketTransport` connects to `ws://...:3000`/`8106` via JSON-RPC. A `system_settings` SQLite table stores configuration with hot-reload via `PUT /api/settings`. React 19 UI in `SettingsView.tsx` provides interactive transport selection and connection testing.

**Tech Stack:** .NET 10, C# 13, `System.Net.WebSockets.ClientWebSocket`, Dapper, SQLite (WAL mode), React 19, TypeScript, Zustand, Vitest, xUnit.

## Global Constraints
- Target Framework: `net10.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- Strict Conventional Commits (`feat:`, `fix:`, `test:`, `chore:`).
- Zero ESLint warnings (`eslint . --max-warnings 0`).
- No hot-patching running containers; containers are immutable.
- Feature branch: `feature/v1.5.0-pluggable-entity-transports-and-ui-settings`.
- Do NOT merge into develop when done; push feature branch and create/prepare PR for review.

---

### Task 1: Core Transport & Capability Interfaces

**Files:**
- Create: `src/CodeMaster.Core/Transports/ITransport.cs`
- Create: `src/CodeMaster.Core/Transports/ILockTransport.cs`
- Create: `src/CodeMaster.Core/Transports/IKeypadTransport.cs`
- Create: `src/CodeMaster.Core/Transports/ISensorTransport.cs`
- Create: `src/CodeMaster.Core/Transports/ITransportRegistry.cs`
- Create: `src/CodeMaster.Core/Transports/TransportModels.cs`
- Test: `tests/CodeMaster.Tests.Unit/TransportRegistryTests.cs`

- [ ] Step 1: Write `TransportModels.cs` defining `TransportStatus`, `ZWaveTransportType`, and event argument records.
- [ ] Step 2: Define `ITransport`, `ILockTransport`, `IKeypadTransport`, `ISensorTransport`, and `ITransportRegistry`.
- [ ] Step 3: Implement `TransportRegistry.cs` in `CodeMaster.Engine/Transports/`.
- [ ] Step 4: Write unit tests in `TransportRegistryTests.cs` asserting registration, capability retrieval, and lookup.
- [ ] Step 5: Run `dotnet test tests/CodeMaster.Tests.Unit` and verify.
- [ ] Step 6: Commit changes: `feat(core): add ITransport abstractions and capability interfaces`.

---

### Task 2: System Settings SQLite Persistence & Repository

**Files:**
- Modify: `src/CodeMaster.Data/Db/DatabaseSeederService.cs`
- Create: `src/CodeMaster.Core/Interfaces/ISettingsRepository.cs`
- Create: `src/CodeMaster.Data/Repositories/SettingsRepository.cs`
- Create: `src/CodeMaster.Core/Services/SystemSettingsService.cs`
- Test: `tests/CodeMaster.Tests.Unit/SettingsRepositoryTests.cs`

- [ ] Step 1: Add `system_settings` table migration in `DatabaseSeederService.cs`.
- [ ] Step 2: Implement `SettingsRepository.cs` using Dapper (`GetAllSettingsAsync`, `GetSettingAsync`, `SetSettingAsync`, `SetSettingsAsync`).
- [ ] Step 3: Implement `SystemSettingsService.cs` providing strongly-typed settings access with environment variable / appsettings fallbacks.
- [ ] Step 4: Write unit tests in `SettingsRepositoryTests.cs`.
- [ ] Step 5: Run tests, verify passing, and commit: `feat(data): add system_settings persistence and configuration fallback service`.

---

### Task 3: Z-Wave WebSocket Transport (`ZWaveWebSocketTransport`)

**Files:**
- Create: `src/CodeMaster.Engine/Transports/ZWaveWebSocketTransport.cs`
- Create: `src/CodeMaster.Engine/Transports/ZWaveJsProtocolModels.cs`
- Test: `tests/CodeMaster.Tests.Unit/ZWaveWebSocketTransportTests.cs`

- [ ] Step 1: Define JSON-RPC message records for `zwave-js-server` in `ZWaveJsProtocolModels.cs`.
- [ ] Step 2: Implement `ZWaveWebSocketTransport` using `ClientWebSocket`:
  - Lifecycle: `StartAsync` with auto-reconnect and exponential backoff, `StopAsync`.
  - Handshake: receive version, send `start_listening`.
  - Commands: CC 98 (Door Lock `set`/`get`), CC 99 (User Code `set`/`get`/`clear`).
  - Event loop: parse `value updated` and `notification` (Ring Keypad entry, jam, manual lock/unlock).
- [ ] Step 3: Implement `ILockTransport` and `IKeypadTransport` methods.
- [ ] Step 4: Write comprehensive unit tests in `ZWaveWebSocketTransportTests.cs` simulating WebSocket server frames.
- [ ] Step 5: Run tests, verify passing, and commit: `feat(engine): implement ZWaveWebSocketTransport with JSON-RPC server driver`.

---

### Task 4: Z-Wave MQTT Transport & Transport Delegation

**Files:**
- Create: `src/CodeMaster.Engine/Transports/ZWaveMqttTransport.cs`
- Modify: `src/CodeMaster.Engine/Providers/Locks/ZWaveJsMqttLockProvider.cs`
- Modify: `src/CodeMaster.Engine/Services/DoorOperationService.cs`
- Test: `tests/CodeMaster.Tests.Unit/ZWaveMqttTransportTests.cs`

- [ ] Step 1: Implement `ZWaveMqttTransport` wrapping Mosquitto `zwave/#` communication.
- [ ] Step 2: Update `DoorOperationService` to resolve the configured lock transport from `ITransportRegistry` before executing lock/unlock.
- [ ] Step 3: Update `HardwareSlotSyncWorker` to sync slots via `ILockTransport`.
- [ ] Step 4: Write unit tests verifying lock operation dispatch through transport registry.
- [ ] Step 5: Run tests, verify passing, and commit: `feat(engine): integrate ITransportRegistry with door operations and hardware slot sync`.

---

### Task 5: Settings API Controller with Dynamic Hot-Reload

**Files:**
- Create: `src/CodeMaster.Web/Controllers/SettingsController.cs`
- Create: `src/CodeMaster.Web/DTOs/SettingsDtos.cs`
- Modify: `src/CodeMaster.Web/Program.cs`
- Test: `tests/CodeMaster.Tests.Unit/SettingsControllerTests.cs`

- [ ] Step 1: Create `SettingsController.cs` with:
  - `GET /api/settings`: Returns effective settings and transport status summary.
  - `PUT /api/settings`: Persists settings and calls `ITransportRegistry.ReconfigureAsync(...)`.
  - `POST /api/settings/test-connection`: Tests WebSocket or MQTT connectivity and returns diagnostics.
- [ ] Step 2: Register services in `Program.cs`.
- [ ] Step 3: Write integration tests in `SettingsControllerTests.cs`.
- [ ] Step 4: Run tests, verify passing, and commit: `feat(web): add SettingsController with dynamic transport hot-reload and diagnostics`.

---

### Task 6: Frontend Settings Store & API Client

**Files:**
- Modify: `src/CodeMaster.UI/src/api/apiClient.ts`
- Modify: `src/CodeMaster.UI/src/types/index.ts`
- Create: `src/CodeMaster.UI/src/stores/useSettingsStore.ts`
- Test: `src/CodeMaster.UI/src/stores/useSettingsStore.test.ts`

- [ ] Step 1: Add settings endpoints and DTOs to `apiClient.ts` and `types/index.ts`.
- [ ] Step 2: Implement Zustand `useSettingsStore.ts` (fetch, update, testConnection, connectionStatus).
- [ ] Step 3: Write tests in `useSettingsStore.test.ts`.
- [ ] Step 4: Run `npm test` and verify passing.
- [ ] Step 5: Commit: `feat(ui): add useSettingsStore and settings API client`.

---

### Task 7: Interactive Settings View Component

**Files:**
- Modify: `src/CodeMaster.UI/src/components/settings/SettingsView.tsx`
- Test: `src/CodeMaster.UI/src/components/settings/SettingsView.test.tsx`

- [ ] Step 1: Rebuild `SettingsView.tsx`:
  - Segmented toggle for Z-Wave mode: `WebSocket (Direct)` vs `MQTT (Mosquitto)`.
  - WebSocket URL input with "Test Connection" button displaying latency and node count.
  - MQTT broker configuration card with live status indicator.
  - Apprise alert webhook configuration.
  - Save button with toast notification.
- [ ] Step 2: Write unit tests in `SettingsView.test.tsx`.
- [ ] Step 3: Run `npm test && npm run lint`.
- [ ] Step 4: Commit: `feat(ui): implement interactive SettingsView with live transport selector and diagnostics`.

---

### Task 8: Door Setup Wizard Node Auto-Discovery

**Files:**
- Modify: `src/CodeMaster.UI/src/components/doors/DoorSetupWizard.tsx`
- Test: `src/CodeMaster.UI/src/components/doors/DoorSetupWizard.test.tsx`

- [ ] Step 1: Update `DoorSetupWizard.tsx` to detect active transport mode.
- [ ] Step 2: In WebSocket mode, offer detected Z-Wave node picker (Node 39 Schlage, Node 40 Ring Keypad).
- [ ] Step 3: Write tests verifying node selection.
- [ ] Step 4: Run `npm run build && npm test && npm run lint`.
- [ ] Step 5: Commit: `feat(ui): enhance DoorSetupWizard with Z-Wave WebSocket node selection`.

---

### Task 9: Live Probe Against Physical Hardware (`ws://10.0.0.10:8106`)

**Files:**
- Test/Probe: Execute live WebSocket probe to `10.0.0.10:8106`.

- [ ] Step 1: Run diagnostics against `ws://10.0.0.10:8106`.
- [ ] Step 2: Verify `start_listening` response from Z-Wave JS UI server.
- [ ] Step 3: Confirm node state and user code CC support for Node 39 (Schlage BE469ZP) and notification events for Node 40 (Ring Keypad v2).

---

### Task 10: End-to-End Verification, Version Bump & PR Push

**Files:**
- Modify: `Directory.Build.props`, `addon/config.yaml`, `addon/CHANGELOG.md`, `package.json`

- [ ] Step 1: Bump SemVer to `1.5.0` across metadata files and update changelog.
- [ ] Step 2: Run `python3 verify_release.py --ci`.
- [ ] Step 3: Commit release assets: `chore(release): bump version to 1.5.0 and prepare PR`.
- [ ] Step 4: Push branch `feature/v1.5.0-pluggable-entity-transports-and-ui-settings` to origin (`git@github.com:spelech/CodeMaster.git`).
- [ ] Step 5: Leave branch open without merging into `develop` as requested.
