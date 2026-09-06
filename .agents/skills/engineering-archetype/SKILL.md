---
name: engineering-archetype
description: Architectural guidelines and master engineering standards for Steven T. Pelech's engineering archetypes across C#, TypeScript, Python, and C++.
---

# Engineering Archetype & Master Guidelines

This skill equips agents to collaborate effectively with Steven T. Pelech across new project setups, feature development, test harness creation, and architectural refactoring.

---

## 🤝 1. Division of Responsibility & Collaboration

- **Steven's Role**: System architecture, structural design, feature conceptualization, domain modeling, requirement specifications, stored procedure/data design, and Mermaid diagram designs.
- **Agent's Role**:
  - **Proactive Questioning**: Ask insightful clarifying questions to flesh out requirements, edge cases, and design constraints since Steven's ideas evolve during design.
  - **Iterative Prototyping**: Build prototypes, run test harnesses with closed-loop feedback, and iterate based on empirical findings.
  - **Branch & PR Discipline**: Work on fresh feature branches off `develop`, make fine-grained Conventional Commits (`feat:`, `fix:`, `test:`), and open PRs that pass all 4-stage CI quality gates before merging into `develop`/`main`.

---

## 🏛️ 2. Core Driving Principles & Code Discipline

1. **SOLID & Modularity**:
   - Strict single responsibility per class/module.
   - Decompose classes exceeding **500 lines of code** into partial classes or distinct sub-services.
   - Build client-focused interfaces (`I*` in C#) even for single implementations to ensure 100% testability.
2. **DRY vs. YAGNI & KISS**:
   - **Rule of Three**: Duplication is acceptable across 2 instances; abstract on the **3rd occurrence**.
   - Do not invent speculative multi-tier frameworks or unused generic abstractions (YAGNI).
3. **Semantic Naming Standard**:
   - **Banned**: `*Manager`, `*Helper`, `*Util`, `*Data` junk drawers.
   - **Enforced**: Role/action-based names (`DatabaseSeederService`, `UserAuthenticator`, `OrderProcessor`, `ServerStatusCard`, `use*Store.ts`).
4. **Efficiency & Performance**:
   - **Database**: Consolidate into a single **Stored Procedure** or multi-result query rather than making 3+ database round-trips.
   - **Frontend**: Mandatory **granular Zustand selectors** (`useServerStore(s => s.servers.length)`) to prevent render cascades.
   - **API**: Provide focused individual HTTP endpoints for decoupled store/component lifecycles.
5. **Error Handling & Diagnostics**:
   - Never leak raw stack traces to API clients.
   - Log rich debug payloads containing the input arguments and state that triggered the error to enable deterministic reproduction.

---

## 💻 3. Polyglot Language Matrix

| Language | Primary Domains | Core Conventions & Libraries |
| :--- | :--- | :--- |
| **C# (.NET 9)** | High-perf systems, control planes, protocols, daemons, native UIs | `System.CommandLine`, full DI, `.slnx`, Dapper + Stored Proc `.sql` files, SQLite WAL (MySQL-like) / MSSQL, Native WPF/WinForms/Avalonia (no Electron), `ConcurrentDictionary`, `Channel<T>`, `SemaphoreSlim`, `Interlocked`, `CancellationToken` throughout. |
| **Python (3.12+)** | Scrapers, data pipelines, vision, ML, automation | `uv`, `pyproject.toml`, FastAPI + FastMCP (Streamable HTTP / SSE), Pydantic v2 schemas, `asyncio`, `pytest` ($\ge$ 80% coverage), `ruff`. |
| **TypeScript / React** | Web UIs, interactive dashboards, browser tools | React + TS strict + Vite, Zustand domain stores, pure CSS Modules + custom properties, bespoke components, `playwright-layout-inspector` 4-point audit. |
| **C++ (C++20/23)** | Algorithms, geometry, native compute, low-level protocol engines | MSBuild (Win) / CMake (Linux), `vcpkg`, strict RAII, smart pointers, GoogleTest (`gtest`), ASan/UBSan, Google Benchmark, C# `[LibraryImport]` / Python `pybind11` interop. |

---

## 🧪 4. Controls & Simulation Testing Framework

1. **Harness Trigger**: Build a dedicated test harness as soon as a project crosses an architectural boundary (API, network, process) or enters algorithm domains with tunable variability.
2. **High-Volume & Closed-Loop Simulation**: Harnesses execute high-volume throughput stress loops, parameter sweeps, and convergence tests.
3. **Disturbance Ingestion**: Inject malformed payloads and abrupt disconnects to verify error fallbacks and clean `CancellationToken` teardowns.
4. **Frontend UI Harness**: Include `data-testid` attributes and Playwright drivers for autonomous agent inspection.
5. **Agent Feedback Envelope**: Format all test failures with:
   - `inputs` & `assumptions`
   - `active_settings`
   - `action_history` (state transitions)
   - `output_delta` (expected vs actual)
   - `captured_logs`
   - `reproduction_command`
6. **Coverage**: Maintain $\ge$ 80% code coverage.

---

## 🚀 5. Multi-Stage GitHub Actions CI/CD (4-Stage Gate)

1. **Gate 1: Release & Link Integrity**: `verify_release.py` verifying SemVer across manifests and relative markdown link integrity.
2. **Gate 2: Parallel Builds & Tests**: Backend build + xUnit/pytest/gtest coverage ($\ge$ 80%); Frontend ESLint (0 warnings) + Vitest.
3. **Gate 3: Fullstack Smoke Gate**: Live background process spawn + `/health` probe loop + live handshake.
4. **Gate 4: Security & Release**: CodeQL multi-language analysis + Docker multi-platform container publish.
