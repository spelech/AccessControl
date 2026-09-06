# CodeMaster Agents Guide

This document provides mandatory architectural guidelines, testing conventions, and execution rules for AI coding agents working in the `CodeMaster` repository.

---

## 🏛️ System Overview & Architecture

**CodeMaster** is a high-performance .NET 10 and React 19 containerized access control and lock/keypad management engine, designed as an open-source grade, scalable replacement for Keymaster with zero Home Assistant entity bloat.

- **Backend Runtime**: .NET 10 (`net10.0`), C# 13, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `codemaster.slnx`.
- **Database & Persistence**: Embedded SQLite in **Write-Ahead Logging (WAL)** mode (`PRAGMA journal_mode = WAL;`) using **Dapper** with parameterized scripts and idempotent migrations in `DatabaseSeederService.cs`.
- **MQTT Pipeline**: `System.Threading.Channels.Channel<MqttInboundMessage>` handles high-throughput ingestion from Mosquitto via `MQTTnet` 4.x.
- **Provider Abstraction**:
  - `ILockProvider`: `ZWaveJsMqttLockProvider`, `GenericMqttLockProvider`, `VirtualLockProvider`.
  - `IKeypadProvider`: `RingMqttKeypadProvider` (Stateless Event), `BuiltInLockKeypadProvider` (Hardware Slotted), `GenericMqttKeypadProvider`.
  - `IDoorSensorProvider`: `MqttContactSensorProvider`.
  - `INotificationDispatcher`: `HomeAssistantDiscoveryService`, `AppriseNotificationDispatcher`.
- **Frontend SPA**: React 19, TypeScript strict mode, Zustand domain stores, pure CSS custom properties (`theme.css`), compiled into `src/CodeMaster.Web/wwwroot/`.
- **Dynamic Ingress**: Fully supports Home Assistant Ingress via dynamic `X-Ingress-Path` header / `<meta name="base-path">` resolution.
- **Model Context Protocol (MCP)**: Implements the **2026-07-28 specification** at `/mcp/sse` (with `2024-11-05` fallback), exposing 6 tools: `codemaster__list_doors`, `codemaster__unlock_door`, `codemaster__lock_door`, `codemaster__create_guest_pin`, `codemaster__revoke_user`, `codemaster__get_access_logs`.

---

## 🤝 Core Engineering Rules & Toolbelt Alignment

1. **Proactive Clarifying Questions**:
   - Steven's ideas evolve during design. Ask insightful, one-at-a-time clarifying questions to nail down requirements, edge cases, and hardware topologies.
2. **Git Branch & PR Discipline**:
   - Work on fresh feature branches off `develop`.
   - Use `develop` as the active integration branch before production release on `main`.
   - Commit using **atomic Conventional Commits** (`feat:`, `fix:`, `test:`, `docs:`, `chore:`).
   - Git remote: `git@github.com:spelech/CodeMaster.git`.
3. **Container Immutability**:
   - **NEVER** hot-patch or edit files in running containers. Deploy strictly via built images (`docker compose up -d --build`).
4. **SOLID & Code Modularity**:
   - Enforce single responsibility. Decompose classes exceeding **500 lines of code**.
   - Build client-focused interfaces (`I*`) for 100% testability.
   - Strictly propagate `CancellationToken` across all async methods.
5. **Semantic Naming Standard**:
   - **Banned**: `*Manager`, `*Helper`, `*Util`, `*Data` junk drawers.
   - **Enforced**: Role/action-based names (`DatabaseSeederService`, `AutoLockStateMachine`, `AccessPolicyEvaluator`, `DoorCard`, `useDoorStore.ts`).
6. **Testing & Layout Inspection**:
   - Maintain $\ge$ 80% code coverage across unit and integration tests.
   - Frontends must pass the 4-point `playwright-layout-inspector` audit (`toHaveNoLayoutOverflow`, `toHaveMobileFit`, `toHaveTouchFriendlyTargets({ minSize: 24 })`, `toPassLayoutAudit({ minScore: 85 })`).
   - Zero ESLint warnings (`eslint . --max-warnings 0`).

---

## 🧪 Common Commands

```bash
# Build whole solution
dotnet build codemaster.slnx

# Run all .NET unit & closed-loop harness tests
dotnet test codemaster.slnx

# Run frontend tests & linting
cd src/CodeMaster.UI && npm test && npm run lint

# Build frontend production bundle into wwwroot
cd src/CodeMaster.UI && npm run build

# Run Playwright layout audit
cd src/CodeMaster.UI && npx playwright test

# Run Release SemVer & Markdown Link Verification
python3 verify_release.py --ci --skip-tests

# Live Docker integration test stack
docker compose -f docker-compose.test.yaml up -d --build
curl -f http://localhost:8155/health
docker compose -f docker-compose.test.yaml down -v

# Atomic commit script
./commit.sh "feat(domain): description of change"
```
