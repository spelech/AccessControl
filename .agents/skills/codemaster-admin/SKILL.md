---
name: codemaster-admin
description: Specialized instructions for managing CodeMaster access points, user identities, PIN credentials, recurring schedules, and access logs via the REST API or the 2026-07-28 Model Context Protocol (MCP) server.
---

# CodeMaster Admin Skill

Use this skill when integrating, testing, or automating CodeMaster doors, users, schedules, or audit logs via the REST API or Model Context Protocol (MCP).

## MCP Server Endpoints

- **SSE Endpoint**: `/mcp/sse`
- **Messages Endpoint**: `/mcp/messages?sessionId=<id>`
- **Protocol Version**: Defaults to `2026-07-28` with negotiated fallback for `2024-11-05`.

## Available MCP Tools

1. `codemaster__list_doors`: Returns all configured doors with live lock state, contact sensor status, and auto-lock countdowns.
2. `codemaster__unlock_door(doorId, durationMinutes)`: Issues remote unlock with optional auto-lock pause.
3. `codemaster__lock_door(doorId)`: Immediately engages the physical lock.
4. `codemaster__create_guest_pin(name, pin, validFrom, validUntil, doorIds)`: Dynamically provisions a temporary guest code with start/end UTC timestamps.
5. `codemaster__revoke_user(userId)`: Revokes all assigned door policies and wipes hardware slots.
6. `codemaster__get_access_logs(doorId, limit)`: Queries access audit logs with user names, timestamps, and unlock methods.

## REST API Endpoints

- `GET /api/doors`: List all doors.
- `POST /api/doors`: Create a door.
- `GET /api/users`: List users, credentials, and door schedules.
- `POST /api/users`: Create a user with PIN and access schedule.
- `GET /api/logs`: Query access audit history.
- `GET /api/logs/stream`: Live Server-Sent Events (SSE) feed of lock/unlock events.
- `GET /health`: Health probe returning HTTP 200 OK.
