# Pluggable Entity Transports & UI Settings Design Specification

## Overview

This specification details the architecture for decoupling hardware communication protocols (transports) from domain entities (locks, keypads, door contact sensors) and introducing an interactive Settings management interface in CodeMaster.

CodeMaster allows users to choose between **WebSocket** (direct connection to Z-Wave JS UI server on `ws://...:3000` / host `8106`) or **MQTT** (Mosquitto `zwave/#` topics) for Z-Wave locks and keypads, while generalizing the transport abstraction across all hardware entities and enabling live configuration via the Web UI.

---

## 1. Architecture & Core Abstractions

### 1.1 Base Transport Interface (`ITransport`)
All hardware communication channels implement `ITransport`:
- `string TransportId { get; }` (e.g. `"zwave_ws"`, `"mqtt_broker"`)
- `string DisplayName { get; }`
- `bool IsConnected { get; }`
- `TransportStatus Status { get; }` (`Connected`, `Connecting`, `Disconnected`, `Degraded`)
- `Task StartAsync(CancellationToken ct = default)`
- `Task StopAsync(CancellationToken ct = default)`
- `event Action<TransportStatusChangedEventArgs>? OnStatusChanged`

### 1.2 Entity-Specific Capabilities
Transports implement capability interfaces depending on the entity types they support:

1. **`ILockTransport`**:
   - `Task<bool> SetLockStateAsync(string deviceTarget, bool locked, CancellationToken ct = default)`
   - `Task<LockState> GetLockStateAsync(string deviceTarget, CancellationToken ct = default)`
   - `Task<bool> SetUserCodeAsync(string deviceTarget, int slot, string pin, string? label, CancellationToken ct = default)`
   - `Task<bool> ClearUserCodeAsync(string deviceTarget, int slot, CancellationToken ct = default)`
   - `Task<IReadOnlyList<HardwareSlotInfo>> GetUserCodesAsync(string deviceTarget, CancellationToken ct = default)`
   - `event Action<LockStateUpdatedEventArgs>? OnLockStateChanged`

2. **`IKeypadTransport`**:
   - `event Action<KeypadEntryEventArgs>? OnKeypadEntry`
   - `Task<bool> SetKeypadModeAsync(string deviceTarget, KeypadArmMode mode, CancellationToken ct = default)`

3. **`ISensorTransport`**:
   - `event Action<DoorSensorStateEventArgs>? OnSensorStateChanged`
   - `Task<DoorContactState> GetSensorStateAsync(string deviceTarget, CancellationToken ct = default)`

### 1.3 `ITransportRegistry`
A thread-safe singleton managing registered transports:
- `void RegisterTransport(ITransport transport)`
- `T? GetTransport<T>(string transportId) where T : class, ITransport`
- `IReadOnlyList<ITransport> GetAllTransports()`
- `Task ReconfigureAsync(SystemSettings settings, CancellationToken ct = default)`

---

## 2. Z-Wave WebSocket Transport (`ZWaveWebSocketTransport`)

Implements `ILockTransport` and `IKeypadTransport` by directly connecting to `zwave-js-server`:
- Protocol: JSON-RPC over WebSocket (`System.Net.WebSockets.ClientWebSocket`).
- Handshake: Connect -> receive `version` event -> send `{ "messageId": "init", "command": "start_listening" }`.
- Lock Commands: Dispatches `endpoint.invoke_cc_api` with Command Class `0x62` (Door Lock).
- Slot Operations: Dispatches `endpoint.invoke_cc_api` with Command Class `0x63` (User Code, methods `set`, `get`, `clear`).
- Event Loop: Dispatches `event: "value updated"` (lock state) and `event: "notification"` (keypad entry / manual operation / jam).
- Fault Tolerance: Exponential backoff reconnect (1s -> 2s -> 4s -> max 15s) with automatic `start_listening` re-subscription.

---

## 3. Dynamic Settings Persistence & API

### 3.1 SQLite Storage (`system_settings`)
A simple key-value table:
```sql
CREATE TABLE IF NOT EXISTS system_settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);
```

### 3.2 Key Configuration Parameters
- `zwave.transport_type`: `"WebSocket"` or `"Mqtt"` (default: `"WebSocket"`)
- `zwave.websocket_url`: `ws://10.0.0.10:8106` (or `ws://zwavejs2mqtt:3000`)
- `zwave.mqtt_prefix`: `"zwave"`
- `mqtt.host`: `"10.0.0.10"`
- `mqtt.port`: `8100` (or `1883`)
- `mqtt.username`: `""`
- `mqtt.password`: `""`
- `apprise.url`: `""`

### 3.3 REST Endpoints (`SettingsController.cs`)
- `GET /api/settings`: Returns effective settings and connection statuses of all registered transports.
- `PUT /api/settings`: Updates settings in SQLite, reconfigures and hot-reconnects active transports without container restarts.
- `POST /api/settings/test-connection`: Tests connectivity to an ad-hoc endpoint (WebSocket or MQTT) and reports latency, driver version, and node count.

---

## 4. Frontend Settings Experience (`CodeMaster.UI`)

1. **`SettingsView.tsx`**:
   - Interactive configuration form with segmented mode selector (**WebSocket (Direct)** vs **MQTT (Mosquitto)**).
   - "Test Connection" button providing real-time feedback (e.g. latency, detected Z-Wave nodes).
   - MQTT broker credentials and Apprise notification settings.
   - "Save & Apply" button invoking `PUT /api/settings`.
2. **`useSettingsStore.ts`**:
   - Zustand store managing settings state, testing status, and live transport health.
3. **`DoorSetupWizard.tsx`**:
   - Dynamic node discovery dropdown in WebSocket mode (e.g. Node 39 Schlage BE469ZP, Node 40 Ring Keypad) or topic selector in MQTT mode.
4. **Header Status Badges**:
   - Live status badges for Z-Wave transport and MQTT broker.
