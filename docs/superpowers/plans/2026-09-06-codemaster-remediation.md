# CodeMaster Remediation & Production Hardening Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform CodeMaster from a vibe-coded prototype into an enterprise-grade access control engine by resolving all critical pipeline disconnects, data orphan bugs, security/encryption gaps, hardware sync safety issues, and frontend streaming limitations across 3 minor releases (v1.1.0, v1.2.0, v1.3.0).

**Architecture:** 
1. Build `MqttInboundConsumerService` consuming `MqttInboundChannel` to process keypad events, lock states, and sensor changes in real time.
2. Fix policy assignment links (`AccessAssignments`) and schedule evaluation.
3. Introduce AES-256-GCM reversible encryption for hardware slot codes and salted PIN hashing.
4. Enforce physical lock clearance on user deletion/revocation.
5. Provide genuine SSE streaming in the React UI, dynamic Ingress basePath resolution, and HA `/data/options.json` support.

**Tech Stack:** .NET 10, C# 13, Dapper, SQLite (WAL), System.Threading.Channels, MQTTnet, React 19, TypeScript, Vitest, Playwright.

---

## Global Constraints
- Target .NET 10 (`net10.0`), C# 13, `<Nullable>enable</Nullable>`.
- Container immutability: all changes build into Docker images cleanly.
- Verify release version synchronization with `python3 verify_release.py --ci --skip-tests`.
- Maintain >= 80% test coverage with zero ESLint warnings (`npm run lint`).
- Only mock when absolutely necessary and ensure mock data matches realistic hardware payloads.

---

## Release Set 1: Core Pipeline & Policy Engine (v1.1.0)
Branch: `feature/v1.1.0-pipeline-and-policy-engine`

### Task 1.1: Build `MqttInboundConsumerService` Background Worker
- [ ] Implement `src/CodeMaster.Engine/Services/MqttInboundConsumerService.cs` as a `BackgroundService` that reads from `_inboundChannel.Reader.ReadAllAsync(stoppingToken)`.
- [ ] Route keypad events to `RingMqttKeypadProvider` and `BuiltInLockKeypadProvider`.
- [ ] On valid keypad entry, call `AccessPolicyEvaluator`, unlock door, update auto-lock state, write `AccessLog`, and broadcast event.
- [ ] Route lock telemetry and door contact states to `DoorOperationService.UpdateDoorStates`.
- [ ] Register `MqttInboundConsumerService` in `Program.cs` and add `codemaster/#` to `MqttOptions.SubscribedTopics`.

### Task 1.2: Auto-Lock Countdown Tracking & State Machine Lifecycle
- [ ] Enhance `AutoLockStateMachine` to track expiration timestamp and `RemainingSeconds`.
- [ ] Update `DoorOperationService.GetRemainingAutoLockSecondsAsync` to query state machine.

### Task 1.3: Fix Policy Assignment in `UsersController.SavePolicy` & `UserManagement.tsx`
- [ ] Modify `SavePolicy` in `UsersController.cs` to assign user to policy and selected/all doors via `AccessAssignment`.
- [ ] Update `useUserStore.ts` and `UserManagement.tsx` to handle door assignments.

### Task 1.4: Hardware Slot Sync Guard & Status Reset
- [ ] Ensure `HardwareSlotSyncWorker.cs` never sends hashed values to physical locks.
- [ ] Fix `HardwareSlotRepository.ClearSlotAsync` to reset `SyncStatus` to `Synced`.

### Task 1.5: Automated Home Assistant Discovery & Release v1.1.0
- [ ] Publish Home Assistant discovery messages on door registration and MQTT connect.
- [ ] Add unit and closed-loop tests verifying the pipeline.
- [ ] Bump version to 1.1.0 and merge to `develop`.

---

## Release Set 2: Cryptographic Security & Hardware Safety (v1.2.0)
Branch: `feature/v1.2.0-security-and-hardware-safety`

### Task 2.1: AES-256-GCM Reversible Encryption Service
- [ ] Implement `ICredentialEncryptionService` using AES-256-GCM.
- [ ] Store authenticated encrypted ciphertext in `EncryptedValue`.

### Task 2.2: Salted PIN Hashing & Constant-Time Verification
- [ ] Generate secure random salts for PIN hashes.
- [ ] Use constant-time equality check (`CryptographicOperations.FixedTimeEquals`).

### Task 2.3: Physical Lock Clearance on Revocation/Deletion
- [ ] When deleting a user in `UsersController` or revoking in MCP `DoorTools`, clear physical lock slots via `lockProvider.ClearSlotCodeAsync`.

### Task 2.4: Timezone Awareness in Access Schedules & Release v1.2.0
- [ ] Support local timezone in `AccessPolicy.IsActiveAt` before checking time/day windows.
- [ ] Bump version to 1.2.0 and merge to `develop`.

---

## Release Set 3: Frontend Live Streaming, Ingress & Integration (v1.3.0)
Branch: `feature/v1.3.0-frontend-streaming-and-ingress`

### Task 3.1: Live SSE Event Streaming in Frontend
- [ ] Connect `EventSource('/api/logs/stream')` in `useAuditStore.ts` and update `LiveEventFeed.tsx`.

### Task 3.2: Dynamic Home Assistant Ingress Base-Path Support
- [ ] Inject `X-Ingress-Path` into `index.html` via ASP.NET middleware.
- [ ] Update `apiClient.getBasePath()` to support Ingress subpaths.

### Task 3.3: Home Assistant OS `options.json` & Apprise Options Fix
- [ ] Load optional `/data/options.json` in `Program.cs`.
- [ ] Fix `AppriseOptions` binding and trigger notifications on access events.
- [ ] Bump version to 1.3.0 and merge to `develop`.
