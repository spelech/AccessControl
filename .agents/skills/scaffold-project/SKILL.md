---
name: scaffold-project
description: Scaffolds a new project adhering to Steven T. Pelech's engineering archetypes (dotnet-fullstack, dotnet-cli, python-fastapi-mcp, react-ts-ui, cpp-algorithms) with living documentation, 4-stage CI/CD, and test harness baselines.
---

# 🚀 Project Scaffolding Skill (`scaffold-project`)

Use this skill when initializing a new repository or service stack.

---

## 📋 Available Archetypes

1. **`dotnet-fullstack`**: C# .NET 9 Backend (Dapper, Stored Procs, SQLite WAL) + React/Zustand Frontend + Playwright Layout Inspector + 4-stage CI/CD.
2. **`dotnet-cli`**: C# .NET 9 Console Utility (`System.CommandLine`, full DI, Native AOT ready, `--json` stream, `-v` debug dumps, `--dry-run`).
3. **`python-fastapi-mcp`**: Python 3.12+ Service (`uv`, FastAPI, FastMCP, Pydantic v2, SQLite `aiosqlite`, `pytest`).
4. **`react-ts-ui`**: Standalone Frontend (React + TS strict + Vite, Zustand stores, pure CSS Modules, `playwright-layout-inspector`).
5. **`cpp-algorithms`**: Native Systems Library (C++20/23, MSBuild/CMake, `vcpkg`, GoogleTest, ASan, Benchmark, C#/Python interop).

---

## ⚡ Execution Modes

### 1. Autonomous Defaults Mode (`--defaults`)
When `--defaults` is passed, the agent immediately generates the project using recommended defaults:
- C# / .NET 9 / Modern `.slnx` solution format.
- SQLite WAL configured with MySQL-compatible types.
- Forward Auth + Bearer tokens for machine auth.
- 4-stage GitHub Actions CI/CD with `verify_release.py` and `commit.sh`.

### 2. Interactive Mode (Default)
When `--defaults` is not supplied, the agent prompts the user with clarifying questions, explicitly presenting the recommended defaults:
- **Project Name & Target Directory**
- **Archetype Selection** (Recommended default presented first)
- **Database Engine** (Recommended: SQLite WAL, with MSSQL/MySQL options)
- **Port Assignment** (if web service)

---

## 📦 Scaffolding Steps Performed by Agent

1. **Create Solution & Project Files**:
   - Solution file (`.slnx`, `CMakeLists.txt`, or `pyproject.toml`).
   - Project dependencies and `Directory.Build.props`.
2. **Scaffold Directory Layout**:
   - `src/` (domain models, interfaces, services, controllers/endpoints, store slices).
   - `tests/` (unit tests + simulation test harness).
3. **Add Living Documentation**:
   - `ARCHITECTURE.md` with Mermaid top-down topology (`flowchart TD`) and sequence diagram (`sequenceDiagram autonumber`).
   - `README.md` with badges, architecture overview, and quickstart commands.
4. **Add CI/CD & Release Automation**:
   - `.github/workflows/ci.yml` (4-stage gate).
   - `commit.sh` and `verify_release.py`.
5. **Add Agent Rules**:
   - Symlink or copy `.toolbelt/rules/AGENTS.md` into repository root.
6. **Verification & Build Smoke**:
   - Run compilation command (`dotnet build`, `pytest`, `npm test`, or `cmake --build`).
   - Verify zero errors and zero linter warnings.
