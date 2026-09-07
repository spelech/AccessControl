using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;
using AccessControl.Core.Security;
using AccessControl.Data.Repositories;
using AccessControl.Mcp.Protocol;

namespace AccessControl.Mcp.Tools;

public interface IDoorTools
{
    IReadOnlyList<McpToolDefinition> GetToolDefinitions();
    Task<McpToolCallResult> ExecuteToolAsync(string toolName, JsonElement? arguments, CancellationToken ct = default);
}

public class DoorTools : IDoorTools
{
    private readonly IAccessPointRepository _doorRepo;
    private readonly IUserRepository _userRepo;
    private readonly ICredentialRepository _credentialRepo;
    private readonly IAccessPolicyRepository _policyRepo;
    private readonly IAuditLogRepository _auditRepo;
    private readonly IHardwareSlotRepository _slotRepo;
    private readonly IDoorOperationService? _doorOps;
    private readonly ICredentialEncryptionService? _encryptionService;

    public DoorTools(
        IAccessPointRepository doorRepo,
        IUserRepository userRepo,
        ICredentialRepository credentialRepo,
        IAccessPolicyRepository policyRepo,
        IAuditLogRepository auditRepo,
        IHardwareSlotRepository slotRepo,
        IDoorOperationService? doorOps = null,
        ICredentialEncryptionService? encryptionService = null)
    {
        _doorRepo = doorRepo;
        _userRepo = userRepo;
        _credentialRepo = credentialRepo;
        _policyRepo = policyRepo;
        _auditRepo = auditRepo;
        _slotRepo = slotRepo;
        _doorOps = doorOps;
        _encryptionService = encryptionService;
    }

    public IReadOnlyList<McpToolDefinition> GetToolDefinitions()
    {
        return
        [
            new McpToolDefinition
            {
                Name = "accesscontrol__list_doors",
                Description = "Returns real-time status of all access points (lock state, contact state, auto-lock state).",
                InputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            },
            new McpToolDefinition
            {
                Name = "accesscontrol__unlock_door",
                Description = "Unlocks an access point with optional auto-lock override duration in minutes.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        doorId = new { type = "string", description = "The unique ID of the access point / door" },
                        durationMinutes = new { type = "number", description = "Optional auto-lock timer override in minutes" }
                    },
                    required = new[] { "doorId" }
                }
            },
            new McpToolDefinition
            {
                Name = "accesscontrol__lock_door",
                Description = "Immediately locks an access point.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        doorId = new { type = "string", description = "The unique ID of the access point / door" }
                    },
                    required = new[] { "doorId" }
                }
            },
            new McpToolDefinition
            {
                Name = "accesscontrol__create_guest_pin",
                Description = "Provisions a temporary guest PIN valid for a specific window across selected doors.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Guest user name" },
                        pin = new { type = "string", description = "4-8 digit numeric PIN code" },
                        doorIds = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "Array of access point door IDs to permit"
                        },
                        validFrom = new { type = "string", description = "ISO 8601 start timestamp" },
                        validUntil = new { type = "string", description = "ISO 8601 expiration timestamp" }
                    },
                    required = new[] { "name", "pin", "doorIds", "validFrom", "validUntil" }
                }
            },
            new McpToolDefinition
            {
                Name = "accesscontrol__revoke_user",
                Description = "Revokes credentials and deactivates a user immediately.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        userId = new { type = "string", description = "The user ID to revoke" }
                    },
                    required = new[] { "userId" }
                }
            },
            new McpToolDefinition
            {
                Name = "accesscontrol__get_access_logs",
                Description = "Retrieves recent access logs with timestamps, user names, and methods.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        doorId = new { type = "string", description = "Optional door ID to filter access logs" },
                        limit = new { type = "integer", description = "Maximum number of logs to return (default 50)" }
                    }
                }
            }
        ];
    }

    public async Task<McpToolCallResult> ExecuteToolAsync(string toolName, JsonElement? arguments, CancellationToken ct = default)
    {
        try
        {
            var normalizedName = toolName.StartsWith("codemaster__", StringComparison.OrdinalIgnoreCase)
                ? "accesscontrol__" + toolName[12..]
                : toolName;

            return normalizedName switch
            {
                "accesscontrol__list_doors" => await ListDoorsAsync(ct),
                "accesscontrol__unlock_door" => await UnlockDoorAsync(arguments, ct),
                "accesscontrol__lock_door" => await LockDoorAsync(arguments, ct),
                "accesscontrol__create_guest_pin" => await CreateGuestPinAsync(arguments, ct),
                "accesscontrol__revoke_user" => await RevokeUserAsync(arguments, ct),
                "accesscontrol__get_access_logs" => await GetAccessLogsAsync(arguments, ct),
                _ => McpToolCallResult.Text($"Unknown tool: '{toolName}'", isError: true)
            };
        }
        catch (Exception ex)
        {
            return McpToolCallResult.Text($"Error executing tool '{toolName}': {ex.Message}", isError: true);
        }
    }

    private async Task<McpToolCallResult> ListDoorsAsync(CancellationToken ct)
    {
        var doors = await _doorRepo.GetAllAsync(ct);
        var doorStatuses = new List<object>();

        foreach (var door in doors)
        {
            var lockState = _doorOps != null ? await _doorOps.GetDoorLockStateAsync(door.Id, ct) : LockState.Locked;
            var contactState = _doorOps != null ? await _doorOps.GetDoorContactStateAsync(door.Id, ct) : DoorContactState.Closed;
            var countdown = _doorOps != null ? await _doorOps.GetRemainingAutoLockSecondsAsync(door.Id, ct) : null;

            doorStatuses.Add(new
            {
                id = door.Id,
                name = door.Name,
                lockState = lockState.ToString(),
                contactState = contactState.ToString(),
                autoLockEnabled = door.AutoLockEnabled,
                autoLockDaySeconds = door.AutoLockDaySeconds,
                autoLockNightSeconds = door.AutoLockNightSeconds,
                remainingCountdownSeconds = countdown,
                lockProviderType = door.LockProviderType,
                keypadProviderType = door.KeypadProviderType,
                doorSensorProviderType = door.DoorSensorProviderType
            });
        }

        var json = JsonSerializer.Serialize(doorStatuses, new JsonSerializerOptions { WriteIndented = true });
        return McpToolCallResult.Text(json);
    }

    private async Task<McpToolCallResult> UnlockDoorAsync(JsonElement? arguments, CancellationToken ct)
    {
        if (arguments == null || !arguments.Value.TryGetProperty("doorId", out var doorIdProp))
        {
            return McpToolCallResult.Text("Missing required parameter: 'doorId'", isError: true);
        }

        var doorId = doorIdProp.GetString();
        if (string.IsNullOrWhiteSpace(doorId))
        {
            return McpToolCallResult.Text("Parameter 'doorId' cannot be empty", isError: true);
        }

        var door = await _doorRepo.GetByIdAsync(doorId, ct);
        if (door == null)
        {
            return McpToolCallResult.Text($"Door '{doorId}' not found", isError: true);
        }

        int? durationMinutes = null;
        if (arguments.Value.TryGetProperty("durationMinutes", out var durProp) && durProp.TryGetInt32(out var minutes))
        {
            durationMinutes = minutes;
        }

        if (_doorOps != null)
        {
            var success = await _doorOps.UnlockDoorAsync(doorId, durationMinutes, ct);
            if (!success)
            {
                return McpToolCallResult.Text($"Failed to unlock door '{door.Name}' ({doorId})", isError: true);
            }
        }

        var log = new AccessLog
        {
            AccessPointId = doorId,
            UserName = "MCP Assistant",
            EventType = AccessEventType.Unlocked,
            Method = AccessMethod.Manual,
            Timestamp = DateTime.UtcNow,
            Details = durationMinutes.HasValue ? $"Unlocked via MCP tool with {durationMinutes.Value}m auto-lock override" : "Unlocked via MCP tool"
        };
        await _auditRepo.InsertAsync(log, ct);

        return McpToolCallResult.Text($"Successfully unlocked door '{door.Name}' ({doorId}).");
    }

    private async Task<McpToolCallResult> LockDoorAsync(JsonElement? arguments, CancellationToken ct)
    {
        if (arguments == null || !arguments.Value.TryGetProperty("doorId", out var doorIdProp))
        {
            return McpToolCallResult.Text("Missing required parameter: 'doorId'", isError: true);
        }

        var doorId = doorIdProp.GetString();
        if (string.IsNullOrWhiteSpace(doorId))
        {
            return McpToolCallResult.Text("Parameter 'doorId' cannot be empty", isError: true);
        }

        var door = await _doorRepo.GetByIdAsync(doorId, ct);
        if (door == null)
        {
            return McpToolCallResult.Text($"Door '{doorId}' not found", isError: true);
        }

        if (_doorOps != null)
        {
            var success = await _doorOps.LockDoorAsync(doorId, ct);
            if (!success)
            {
                return McpToolCallResult.Text($"Failed to lock door '{door.Name}' ({doorId})", isError: true);
            }
        }

        var log = new AccessLog
        {
            AccessPointId = doorId,
            UserName = "MCP Assistant",
            EventType = AccessEventType.Locked,
            Method = AccessMethod.Manual,
            Timestamp = DateTime.UtcNow,
            Details = "Locked via MCP tool"
        };
        await _auditRepo.InsertAsync(log, ct);

        return McpToolCallResult.Text($"Successfully locked door '{door.Name}' ({doorId}).");
    }

    private async Task<McpToolCallResult> CreateGuestPinAsync(JsonElement? arguments, CancellationToken ct)
    {
        if (arguments == null)
        {
            return McpToolCallResult.Text("Missing arguments object", isError: true);
        }

        var args = arguments.Value;
        if (!args.TryGetProperty("name", out var nameProp) || string.IsNullOrWhiteSpace(nameProp.GetString()))
        {
            return McpToolCallResult.Text("Missing required parameter: 'name'", isError: true);
        }
        if (!args.TryGetProperty("pin", out var pinProp) || string.IsNullOrWhiteSpace(pinProp.GetString()))
        {
            return McpToolCallResult.Text("Missing required parameter: 'pin'", isError: true);
        }
        if (!args.TryGetProperty("validFrom", out var fromProp) || !DateTime.TryParse(fromProp.GetString(), out var validFrom))
        {
            return McpToolCallResult.Text("Missing or invalid ISO date: 'validFrom'", isError: true);
        }
        if (!args.TryGetProperty("validUntil", out var untilProp) || !DateTime.TryParse(untilProp.GetString(), out var validUntil))
        {
            return McpToolCallResult.Text("Missing or invalid ISO date: 'validUntil'", isError: true);
        }
        if (!args.TryGetProperty("doorIds", out var doorsProp) || doorsProp.ValueKind != JsonValueKind.Array)
        {
            return McpToolCallResult.Text("Missing required array: 'doorIds'", isError: true);
        }

        var doorIds = new List<string>();
        foreach (var item in doorsProp.EnumerateArray())
        {
            var did = item.GetString();
            if (!string.IsNullOrWhiteSpace(did))
            {
                doorIds.Add(did);
            }
        }

        if (doorIds.Count == 0)
        {
            return McpToolCallResult.Text("At least one doorId must be provided in 'doorIds'", isError: true);
        }

        var name = nameProp.GetString()!;
        var pin = pinProp.GetString()!;

        if (pin.Length < 4 || pin.Length > 8 || !pin.All(char.IsAsciiDigit))
        {
            return McpToolCallResult.Text("PIN must be between 4 and 8 numeric digits (0-9).", isError: true);
        }

        // 1. Create User
        var user = new User
        {
            Name = name,
            Role = UserRole.Guest,
            IsActive = true
        };
        await _userRepo.InsertAsync(user, ct);

        // 2. Encrypt PIN and compute salted hash
        var encryptedPin = _encryptionService != null ? _encryptionService.Encrypt(pin) : pin;
        var saltedHash = PinSecurityHelper.CreateSaltedHash(pin);

        var credential = new Credential
        {
            UserId = user.Id,
            Type = CredentialType.PIN,
            EncryptedValue = encryptedPin,
            HashedValue = saltedHash,
            PinLength = pin.Length,
            Label = "Guest PIN"
        };
        await _credentialRepo.InsertAsync(credential, ct);

        // 3. Create Access Policy
        var policy = new AccessPolicy
        {
            Name = $"{name} Guest Window",
            ScheduleType = ScheduleType.DateRange,
            ValidFrom = validFrom.ToUniversalTime(),
            ValidUntil = validUntil.ToUniversalTime(),
            IsEnabled = true
        };
        await _policyRepo.InsertAsync(policy, ct);

        // 4. Assign Policy to Doors
        foreach (var doorId in doorIds)
        {
            var assignment = new AccessAssignment
            {
                AccessPointId = doorId,
                UserId = user.Id,
                PolicyId = policy.Id
            };
            await _policyRepo.AssignPolicyAsync(assignment, ct);
        }

        var resultObj = new
        {
            status = "created",
            userId = user.Id,
            userName = user.Name,
            credentialId = credential.Id,
            policyId = policy.Id,
            validFrom = validFrom.ToUniversalTime().ToString("o"),
            validUntil = validUntil.ToUniversalTime().ToString("o"),
            doors = doorIds
        };

        return McpToolCallResult.Text(JsonSerializer.Serialize(resultObj, new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task<McpToolCallResult> RevokeUserAsync(JsonElement? arguments, CancellationToken ct)
    {
        if (arguments == null || !arguments.Value.TryGetProperty("userId", out var userProp))
        {
            return McpToolCallResult.Text("Missing required parameter: 'userId'", isError: true);
        }

        var userId = userProp.GetString();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return McpToolCallResult.Text("Parameter 'userId' cannot be empty", isError: true);
        }

        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user == null)
        {
            return McpToolCallResult.Text($"User '{userId}' not found", isError: true);
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);

        var clearedSlotsCount = 0;
        if (_doorOps != null)
        {
            clearedSlotsCount = await _doorOps.ClearUserHardwareSlotsAsync(userId, ct);
        }
        else
        {
            var doors = await _doorRepo.GetAllAsync(ct);
            foreach (var door in doors)
            {
                var slots = await _slotRepo.GetSlotsForDoorAsync(door.Id, ct);
                foreach (var slot in slots.Where(s => s.UserId == userId))
                {
                    await _slotRepo.ClearSlotAsync(slot.Id, ct);
                    clearedSlotsCount++;
                }
            }
        }

        return McpToolCallResult.Text($"User '{user.Name}' ({userId}) revoked successfully. Cleared {clearedSlotsCount} hardware slots.");
    }

    private async Task<McpToolCallResult> GetAccessLogsAsync(JsonElement? arguments, CancellationToken ct)
    {
        string? doorId = null;
        var limit = 50;

        if (arguments.HasValue)
        {
            if (arguments.Value.TryGetProperty("doorId", out var dProp) && !string.IsNullOrWhiteSpace(dProp.GetString()))
            {
                doorId = dProp.GetString();
            }

            if (arguments.Value.TryGetProperty("limit", out var lProp) && lProp.TryGetInt32(out var lVal))
            {
                limit = Math.Clamp(lVal, 1, 500);
            }
        }

        IReadOnlyList<AccessLog> logs = !string.IsNullOrWhiteSpace(doorId)
            ? await _auditRepo.GetRecentLogsAsync(doorId, limit, ct)
            : await _auditRepo.GetAllRecentLogsAsync(limit, ct);

        var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });
        return McpToolCallResult.Text(json);
    }
}
