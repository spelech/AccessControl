---
name: codemaster-engine
description: Specialized instructions for developing, testing, and debugging CodeMaster hardware providers, MQTT ingestion pipelines, auto-lock state machines, and hardware slot sync loops.
---

# CodeMaster Engine Skill

Use this skill when extending lock providers, keypad providers, door contact sensor parsing, auto-lock logic, or MQTT topic dispatching in `CodeMaster.Engine`.

## Architecture & Subsystems

1. **Provider Contracts**:
   - `ILockProvider`: Implementations in `src/CodeMaster.Engine/Providers/Locks/`. Must declare `LockCapabilities` and handle asynchronous lock/unlock and slot code synchronization.
   - `IKeypadProvider`: Implementations in `src/CodeMaster.Engine/Providers/Keypads/`. Declare `KeypadMode` (`StatelessEvent` like Ring Keypad vs `HardwareSlotted` like Schlage deadbolts).
   - `IDoorSensorProvider`: Implementations in `src/CodeMaster.Engine/Providers/Sensors/`. Handle contact state parsing (`Closed` vs `Open`).

2. **Inbound Channel**:
   - High-throughput `System.Threading.Channels.Channel<MqttInboundMessage>` handles messages received from Mosquitto via `MQTTnet`.

3. **AutoLock State Machine**:
   - Door contact state dictates countdown behavior:
     - Lock unlocked + door open -> `AutoLockStatus.PausedDoorOpen`.
     - Lock unlocked + door closed -> `AutoLockStatus.CountingDown`.
     - Door opened during countdown -> cancels timer.
     - On timeout -> dispatches lock command.
     - On jam -> executes 1 retry after 5s before firing alert.

## Testing Standards

- All new provider logic or state machine changes MUST be accompanied by xUnit tests in `tests/CodeMaster.Tests.Unit/` or closed-loop tests in `tests/CodeMaster.Tests.Harness/`.
- Run full test suite:
  ```bash
  dotnet test codemaster.slnx
  ```
