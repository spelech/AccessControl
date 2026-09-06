# Changelog

All notable changes to the CodeMaster Home Assistant Add-on will be documented in this file.

## [1.4.0] - 2026-09-06

### Added
- In-modal PIN validation error alert banner (`role="alert"`) and visual border highlighting in `UserManagement` UI with auto-dismiss on keystroke.
- Strict 4–8 numeric digit PIN validation in REST API (`UsersController.SetPin`) and MCP tool (`codemaster__create_guest_pin`).
- Closed-loop test harness assertions replacing trivial checks: live SSE event delivery stream tests, hardware slot error handling, and non-numeric credential rejection tests.

### Fixed
- Fixed SSE event serialization formatting (`s_sseJsonOptions`) using camelCase naming policy and string enum serialization for real-time frontend streaming.

## [1.3.0] - 2026-09-06

### Added
- Real-time live activity audit streaming via Server-Sent Events (`EventSource`) at `/api/logs/stream` integrated with Zustand `useAuditStore`.
- Home Assistant dynamic Ingress reverse-proxy integration with automated `<meta name="base-path">` and `<base href>` runtime HTML injection.
- Home Assistant Add-on `/data/options.json` configuration ingestion mapping `mqtt_host`, `mqtt_port`, `mqtt_username`, and `mqtt_password` directly to engine options.
- Manual lock/unlock audit event dispatch to `INotificationDispatcher` (Apprise) with typed client constructor resolution.

## [1.2.0] - 2026-09-06

### Added
- Authenticated AES-256-GCM `ICredentialEncryptionService` protecting sensitive PIN credentials at rest with dynamic key derivation and transparent legacy plaintext fallback.
- Cryptographically salted PIN hashing with constant-time equality verification (`PinSecurityHelper.VerifyPinHash`) preventing timing attacks.
- Physical Lock Code Revocation on user deletion and MCP revocation (`ClearUserHardwareSlotsAsync`) clearing active lock deadbolt slot codes.
- Timezone-aware access policy evaluation supporting arbitrary `TimeZoneId` mappings (e.g. `America/Chicago`) for localized recurring schedules.

## [1.1.0] - 2026-09-06

### Added
- Hosted `MqttInboundConsumerService` for reliable continuous consumption and processing of the bounded MQTT channel.
- Automatic Door Policy Assignments in `UsersController.Create` and `SavePolicy` with multi-door targeting.
- Automated Home Assistant MQTT Discovery entity registration and unregistration on door lifecycle events.
- Live real-time lock state and contact sensor telemetry reconciliation via `UpdateDoorStates`.

### Fixed
- Fixed auto-lock countdown calculation to expose live remaining seconds via dynamic expiration timestamp.
- Added numeric PIN validation (`All(char.IsAsciiDigit)`) in `HardwareSlotSyncWorker` preventing raw hashes being sent to physical locks.
- Fixed `HardwareSlotRepository.ClearSlotAsync` marking slots as Synced instead of PendingSync.

## [1.0.0] - 2026-09-06

### Added
- Initial release of CodeMaster Home Assistant Add-on.
- Full Home Assistant Ingress integration (`ingress: true`) on port 8150.
- Native 64-bit container support (`linux/amd64`).
- Automatic MQTT broker connection to `core-mosquitto:1883` with schema validation.
- Responsive React 19 administrative dashboard with dark mode and mobile-first design.
- Universal access control engine supporting Z-Wave, Zigbee, Ring, and Matter devices.
- Embedded SQLite Write-Ahead Logging (WAL) database persistence.
- Home Assistant MQTT Discovery protocol for clean, unbloated door entities.
