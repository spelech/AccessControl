# Design Specification: CodeMaster to AccessControl Full Project Rename

**Date**: 2026-09-06  
**Status**: Approved (Autonomous Execution Goal)  
**Author**: Antigravity Agent & Steven T. Pelech  
**Target Version**: 1.5.1  

---

## 1. Executive Summary & Rationale

**CodeMaster** was originally created as an externalized, high-performance C# / .NET 10 alternative to Home Assistant's *Keymaster* integration, designed to eliminate entity explosion while managing smart door lock PIN codes and hardware slots. 

However, in developer and homelab ecosystems, "CodeMaster" creates strong semantic confusion with software engineering tools (source code editors, IDEs, code linters, or gaming studios). Physical security and smart home hardware management is about **physical access control, keypad credentials, auto-lock state machines, and perimeter security**, not source code.

This specification outlines the complete, atomic rename of the codebase, .NET solution, projects, namespaces, frontend UI, Home Assistant add-on, Docker containers, MCP tools, CI/CD workflows, GitHub repository, and local development path to **AccessControl**.

---

## 2. Global Identity & Identifier Matrix

| Component | Old Identity (`CodeMaster`) | New Identity (`AccessControl`) |
| :--- | :--- | :--- |
| **Product Name** | CodeMaster | AccessControl |
| **GitHub Repository** | `spelech/CodeMaster` | `spelech/AccessControl` |
| **GitHub Container Registry** | `ghcr.io/spelech/codemaster` | `ghcr.io/spelech/accesscontrol` |
| **Solution File** | `codemaster.slnx` | `AccessControl.slnx` |
| **C# Root Namespace** | `CodeMaster.*` | `AccessControl.*` |
| **C# Core Project** | `src/CodeMaster.Core` | `src/AccessControl.Core` |
| **C# Data Project** | `src/CodeMaster.Data` | `src/AccessControl.Data` |
| **C# Engine Project** | `src/CodeMaster.Engine` | `src/AccessControl.Engine` |
| **C# MCP Project** | `src/CodeMaster.Mcp` | `src/AccessControl.Mcp` |
| **C# Web Host Project** | `src/CodeMaster.Web` | `src/AccessControl.Web` |
| **C# Unit Tests Project** | `tests/CodeMaster.Tests.Unit` | `tests/AccessControl.Tests.Unit` |
| **C# Harness Tests Project** | `tests/CodeMaster.Tests.Harness` | `tests/AccessControl.Tests.Harness` |
| **C# Simulator Project** | `tests/CodeMaster.Tests.Simulator` | `tests/AccessControl.Tests.Simulator` |
| **Frontend UI App** | `src/CodeMaster.UI` | `src/AccessControl.UI` |
| **Frontend Package Name** | `codemaster-ui` | `accesscontrol-ui` |
| **Frontend Layout Audit** | `tests/CodeMaster.UI.Tests` | `tests/AccessControl.UI.Tests` |
| **Home Assistant Add-on Slug** | `codemaster` | `accesscontrol` |
| **Home Assistant Add-on Name** | `CodeMaster` | `AccessControl` |
| **Docker Container Name** | `codemaster` | `accesscontrol` |
| **Default SQLite Database** | `codemaster.db` | `accesscontrol.db` (auto-migrating) |
| **Default MQTT Client ID** | `codemaster` | `accesscontrol` |
| **MCP Tool Prefix** | `codemaster__*` | `accesscontrol__*` |
| **Host Ports (Preserved)** | `8155` (Web / Ingress), `8150` (Internal) | `8155` (Web / Ingress), `8150` (Internal) |
| **Dev Filesystem Path** | `/containers/dev/codemaster` | `/containers/dev/accesscontrol` (with symlink) |

---

## 3. Detailed Rename Architecture

### 3.1. .NET Solution & Project Renaming
1. **Rename directory and `.csproj` files**:
   - `src/CodeMaster.Core/CodeMaster.Core.csproj` -> `src/AccessControl.Core/AccessControl.Core.csproj`
   - `src/CodeMaster.Data/CodeMaster.Data.csproj` -> `src/AccessControl.Data/AccessControl.Data.csproj`
   - `src/CodeMaster.Engine/CodeMaster.Engine.csproj` -> `src/AccessControl.Engine/AccessControl.Engine.csproj`
   - `src/CodeMaster.Mcp/CodeMaster.Mcp.csproj` -> `src/AccessControl.Mcp/AccessControl.Mcp.csproj`
   - `src/CodeMaster.Web/CodeMaster.Web.csproj` -> `src/AccessControl.Web/AccessControl.Web.csproj`
   - `tests/CodeMaster.Tests.Unit/CodeMaster.Tests.Unit.csproj` -> `tests/AccessControl.Tests.Unit/AccessControl.Tests.Unit.csproj`
   - `tests/CodeMaster.Tests.Harness/CodeMaster.Tests.Harness.csproj` -> `tests/AccessControl.Tests.Harness/AccessControl.Tests.Harness.csproj`
   - `tests/CodeMaster.Tests.Simulator/CodeMaster.Tests.Simulator.csproj` -> `tests/AccessControl.Tests.Simulator/AccessControl.Tests.Simulator.csproj`
2. **Rename Solution File**:
   - `codemaster.slnx` -> `AccessControl.slnx`. Update all XML path entries to point to `AccessControl.*`.
3. **Namespace Refactor**:
   - Global replace across all C# `.cs` files:
     - `namespace CodeMaster.` -> `namespace AccessControl.`
     - `using CodeMaster.` -> `using AccessControl.`
     - `CodeMaster.Web` -> `AccessControl.Web`
     - Project reference paths in `.csproj` files updated from `CodeMaster.*` to `AccessControl.*`.

### 3.2. Data Persistence & Backward Compatibility
- In `AccessControl.Web/Program.cs`:
  - Check for legacy `codemaster.db` and migrate if `accesscontrol.db` does not exist yet.
  - Connection string default updated to `Data Source=accesscontrol.db`.
  - Default MQTT ClientId updated to `accesscontrol`.

### 3.3. Model Context Protocol (MCP) Tool Suite
- Rename service registration: `services.AddAccessControlMcp()`.
- Update all 6 MCP tool names:
  - `codemaster__list_doors` -> `accesscontrol__list_doors`
  - `codemaster__unlock_door` -> `accesscontrol__unlock_door`
  - `codemaster__lock_door` -> `accesscontrol__lock_door`
  - `codemaster__create_guest_pin` -> `accesscontrol__create_guest_pin`
  - `codemaster__revoke_user` -> `accesscontrol__revoke_user`
  - `codemaster__get_access_logs` -> `accesscontrol__get_access_logs`
- Provide backward compatibility alias handling in MCP routing if a legacy `codemaster__*` tool is requested.

### 3.4. Frontend UI Refactoring
- Rename folder `src/CodeMaster.UI` -> `src/AccessControl.UI`.
- Rename layout audit test folder `tests/CodeMaster.UI.Tests` -> `tests/AccessControl.UI.Tests`.
- `package.json`:
  - `"name": "accesscontrol-ui"`
- UI Strings & Headers:
  - Update navigation bar title, HTML title tag (`index.html`), and branding headers from "CodeMaster" to "AccessControl".
  - Update `useSettingsStore` and setup wizard labels.
- Build output destination: compiled directly into `src/AccessControl.Web/wwwroot/`.

### 3.5. Home Assistant Add-on & Docker Containers
- `addon/config.yaml`:
  - `name: "AccessControl"`
  - `slug: "accesscontrol"`
  - `image: "ghcr.io/spelech/accesscontrol"`
- `addon/DOCS.md` & `addon/CHANGELOG.md`:
  - Update all references and documentation to AccessControl.
- `addon/Dockerfile` & `Dockerfile`:
  - Update project paths to `src/AccessControl.Web/AccessControl.Web.csproj`.
- `docker-compose.yaml` & `docker-compose.test.yaml`:
  - Service name: `accesscontrol`
  - Container name: `accesscontrol`
  - Image: `ghcr.io/spelech/accesscontrol:latest`
  - Environment: `DATABASE__PATH=/app/data/accesscontrol.db`, `MQTT__CLIENTID=accesscontrol`

### 3.6. CI/CD Workflows & Scripts
- `.github/workflows/ci.yml`:
  - Update project paths, build paths, test report artifact names, and smoke run commands to `AccessControl.Web`.
- `.github/workflows/docker-publish.yml`:
  - Target image: `ghcr.io/spelech/accesscontrol`.
- `.github/workflows/codeql.yml`:
  - Build command updated for `AccessControl.slnx`.
- `commit.sh` & `verify_release.py`:
  - Update project paths and references to `src/AccessControl.UI/package.json` and `AccessControl.slnx`.

### 3.7. GitHub Repository & Local Paths
- Use `gh repo rename AccessControl --yes` to rename GitHub repository.
- Update git remote origin URL: `git@github.com:spelech/AccessControl.git`.
- Local folder: Symlink `/containers/dev/codemaster` <-> `/containers/dev/accesscontrol` to ensure uninterrupted CLI tool execution.

---

## 4. Verification Plan

1. **Solution Build**: `dotnet build AccessControl.slnx --configuration Release` must compile with 0 warnings, 0 errors.
2. **Backend Unit Tests**: `dotnet test AccessControl.slnx --configuration Release` (97 unit tests passing).
3. **Synthetic Harness**: All closed-loop Ring Keypad and hardware simulation tests passing.
4. **Frontend Test Suite**: `npm test && npm run lint` in `src/AccessControl.UI` (53 tests passing, 0 ESLint warnings).
5. **Frontend Production Build**: `npm run build` generates valid bundle in `src/AccessControl.Web/wwwroot/`.
6. **Release & Link Verification**: `python3 verify_release.py --ci --skip-tests` passing cleanly.
7. **Smoke Test**: Local execution of `AccessControl.Web` responds with `{"status":"healthy"}` on `/health`.
8. **Git & CI Verification**: Push to GitHub and verify GitHub Actions CI quality gates pass.
