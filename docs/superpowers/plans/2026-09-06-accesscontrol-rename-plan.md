# Implementation Plan: Full Project Rename to AccessControl

**Date**: 2026-09-06  
**Status**: In Progress  
**Spec**: `docs/superpowers/specs/2026-09-06-accesscontrol-rename-design.md`  

---

## Phase 1: .NET Solution, Projects & Namespaces Refactoring
- [ ] Rename C# project directories and `.csproj` files:
  - `src/CodeMaster.Core` -> `src/AccessControl.Core` (`AccessControl.Core.csproj`)
  - `src/CodeMaster.Data` -> `src/AccessControl.Data` (`AccessControl.Data.csproj`)
  - `src/CodeMaster.Engine` -> `src/AccessControl.Engine` (`AccessControl.Engine.csproj`)
  - `src/CodeMaster.Mcp` -> `src/AccessControl.Mcp` (`AccessControl.Mcp.csproj`)
  - `src/CodeMaster.Web` -> `src/AccessControl.Web` (`AccessControl.Web.csproj`)
  - `tests/CodeMaster.Tests.Unit` -> `tests/AccessControl.Tests.Unit` (`AccessControl.Tests.Unit.csproj`)
  - `tests/CodeMaster.Tests.Harness` -> `tests/AccessControl.Tests.Harness` (`AccessControl.Tests.Harness.csproj`)
  - `tests/CodeMaster.Tests.Simulator` -> `tests/AccessControl.Tests.Simulator` (`AccessControl.Tests.Simulator.csproj`)
- [ ] Update all project references inside `.csproj` files.
- [ ] Create `AccessControl.slnx` replacing `codemaster.slnx`.
- [ ] Batch refactor C# code files:
  - Replace `namespace CodeMaster.` with `namespace AccessControl.`
  - Replace `using CodeMaster.` with `using AccessControl.`
- [ ] Build solution: `dotnet build AccessControl.slnx`.
- [ ] Run backend tests: `dotnet test AccessControl.slnx`.

## Phase 2: MCP Tools, Database & Configuration
- [ ] Update MCP tool definitions to `accesscontrol__*` with backward-compatible aliases for `codemaster__*`.
- [ ] Update extension method `AddAccessControlMcp()`.
- [ ] Add auto-migration logic from `codemaster.db` to `accesscontrol.db` in `Program.cs`.
- [ ] Update default MQTT ClientId to `accesscontrol`.
- [ ] Update `appsettings.json` and default connection strings.

## Phase 3: Frontend UI Refactoring
- [ ] Rename `src/CodeMaster.UI` -> `src/AccessControl.UI`.
- [ ] Rename `tests/CodeMaster.UI.Tests` -> `tests/AccessControl.UI.Tests`.
- [ ] Update `package.json` package name to `accesscontrol-ui`.
- [ ] Update branding strings in UI components, headers, HTML title.
- [ ] Run `npm test && npm run lint` in `src/AccessControl.UI`.
- [ ] Build production bundle: `npm run build` into `src/AccessControl.Web/wwwroot/`.

## Phase 4: Container Definitions, Add-on, CI/CD & Scripts
- [ ] Update `addon/config.yaml`, `addon/DOCS.md`, `addon/Dockerfile`, `addon/build.yaml`.
- [ ] Update root `Dockerfile`, `docker-compose.yaml`, `docker-compose.test.yaml`.
- [ ] Update `.github/workflows/ci.yml`, `codeql.yml`, `docker-publish.yml`.
- [ ] Update `verify_release.py` and `commit.sh`.
- [ ] Update `README.md`, `ARCHITECTURE.md`, `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`.
- [ ] Run `python3 verify_release.py --ci --skip-tests`.

## Phase 5: GitHub Repo Rename & Remote Synchronization
- [ ] Rename GitHub repository using `gh repo rename AccessControl --yes`.
- [ ] Update git remote `origin` to `git@github.com:spelech/AccessControl.git`.

## Phase 6: Commit, Push, PR & CI Quality Gate Verification
- [ ] Commit all changes atomically.
- [ ] Push `feature/accesscontrol-full-rename` to origin.
- [ ] Create Pull Request targeting `develop`.
- [ ] Watch CI Quality Gates until 100% green.
- [ ] Merge PR into `develop`.

## Phase 7: Local Filesystem Symlink
- [ ] Create `/containers/dev/accesscontrol` symlink ensuring continuous compatibility.
