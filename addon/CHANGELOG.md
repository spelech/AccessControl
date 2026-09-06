# Changelog

All notable changes to the CodeMaster Home Assistant Add-on will be documented in this file.

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
