using System.Security.Cryptography;
using System.Text;
using CodeMaster.Core.Models;
using CodeMaster.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CodeMaster.Web.Controllers;

public class CreateUserRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Member;
    public string? GroupId { get; set; }
    public string? Pin { get; set; }
    public string? PinLabel { get; set; }
    public List<string>? DoorIds { get; set; }
    public AccessPolicy? Policy { get; set; }
}

public class SetPinRequest
{
    public string Pin { get; set; } = string.Empty;
    public string? Label { get; set; } = "PIN";
}

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepo;
    private readonly ICredentialRepository _credentialRepo;
    private readonly IAccessPolicyRepository _policyRepo;
    private readonly IHardwareSlotRepository _slotRepo;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserRepository userRepo,
        ICredentialRepository credentialRepo,
        IAccessPolicyRepository policyRepo,
        IHardwareSlotRepository slotRepo,
        ILogger<UsersController> logger)
    {
        _userRepo = userRepo;
        _credentialRepo = credentialRepo;
        _policyRepo = policyRepo;
        _slotRepo = slotRepo;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var users = await _userRepo.GetAllAsync(ct);
        var result = new List<object>();

        foreach (var user in users)
        {
            var credentials = await _credentialRepo.GetByUserIdAsync(user.Id, ct);
            var policies = await _policyRepo.GetByUserIdAsync(user.Id, ct);

            result.Add(new
            {
                id = user.Id,
                name = user.Name,
                role = user.Role.ToString(),
                isActive = user.IsActive,
                groupId = user.GroupId,
                createdAt = user.CreatedAt,
                updatedAt = user.UpdatedAt,
                credentials = credentials.Select(c => new
                {
                    id = c.Id,
                    userId = c.UserId,
                    type = c.Type.ToString(),
                    pinLength = c.PinLength,
                    label = c.Label,
                    createdAt = c.CreatedAt
                }),
                policies = policies.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    scheduleType = p.ScheduleType.ToString(),
                    daysOfWeek = p.DaysOfWeek,
                    startTime = p.StartTime,
                    endTime = p.EndTime,
                    validFrom = p.ValidFrom,
                    validUntil = p.ValidUntil,
                    remainingUses = p.RemainingUses,
                    isEnabled = p.IsEnabled
                })
            });
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(id, ct);
        if (user == null)
        {
            return NotFound(new { error = $"User '{id}' not found" });
        }

        var credentials = await _credentialRepo.GetByUserIdAsync(id, ct);
        var policies = await _policyRepo.GetByUserIdAsync(id, ct);

        return Ok(new
        {
            id = user.Id,
            name = user.Name,
            role = user.Role.ToString(),
            isActive = user.IsActive,
            groupId = user.GroupId,
            createdAt = user.CreatedAt,
            updatedAt = user.UpdatedAt,
            credentials = credentials.Select(c => new
            {
                id = c.Id,
                userId = c.UserId,
                type = c.Type.ToString(),
                pinLength = c.PinLength,
                label = c.Label,
                createdAt = c.CreatedAt
            }),
            policies = policies.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                scheduleType = p.ScheduleType.ToString(),
                daysOfWeek = p.DaysOfWeek,
                startTime = p.StartTime,
                endTime = p.EndTime,
                validFrom = p.ValidFrom,
                validUntil = p.ValidUntil,
                remainingUses = p.RemainingUses,
                isEnabled = p.IsEnabled
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "User name is required" });
        }

        var user = new User
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString() : request.Id,
            Name = request.Name,
            Role = request.Role,
            GroupId = request.GroupId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepo.InsertAsync(user, ct);
        _logger.LogInformation("Created user '{Name}' ({Id})", user.Name, user.Id);

        if (!string.IsNullOrWhiteSpace(request.Pin))
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.Pin));
            var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

            var cred = new Credential
            {
                UserId = user.Id,
                Type = CredentialType.PIN,
                EncryptedValue = request.Pin,
                HashedValue = hashHex,
                PinLength = request.Pin.Length,
                Label = request.PinLabel ?? "PIN",
                CreatedAt = DateTime.UtcNow
            };
            await _credentialRepo.InsertAsync(cred, ct);
        }

        if (request.Policy != null)
        {
            if (string.IsNullOrWhiteSpace(request.Policy.Id))
            {
                request.Policy.Id = Guid.NewGuid().ToString();
            }
            await _policyRepo.InsertAsync(request.Policy, ct);

            if (request.DoorIds != null)
            {
                foreach (var doorId in request.DoorIds)
                {
                    var assignment = new AccessAssignment
                    {
                        AccessPointId = doorId,
                        UserId = user.Id,
                        PolicyId = request.Policy.Id
                    };
                    await _policyRepo.AssignPolicyAsync(assignment, ct);
                }
            }
        }

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] User user, CancellationToken ct)
    {
        var existing = await _userRepo.GetByIdAsync(id, ct);
        if (existing == null)
        {
            return NotFound(new { error = $"User '{id}' not found" });
        }

        user.Id = id;
        user.UpdatedAt = DateTime.UtcNow;
        user.CreatedAt = existing.CreatedAt;

        await _userRepo.UpdateAsync(user, ct);
        _logger.LogInformation("Updated user '{Name}' ({Id})", user.Name, id);

        return Ok(user);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var existing = await _userRepo.GetByIdAsync(id, ct);
        if (existing == null)
        {
            return NotFound(new { error = $"User '{id}' not found" });
        }

        await _userRepo.DeleteAsync(id, ct);
        _logger.LogInformation("Deleted user '{Name}' ({Id})", existing.Name, id);

        return NoContent();
    }

    [HttpGet("{id}/credentials")]
    public async Task<IActionResult> GetCredentials(string id, CancellationToken ct)
    {
        var credentials = await _credentialRepo.GetByUserIdAsync(id, ct);
        return Ok(credentials.Select(c => new
        {
            id = c.Id,
            userId = c.UserId,
            type = c.Type.ToString(),
            pinLength = c.PinLength,
            label = c.Label,
            createdAt = c.CreatedAt
        }));
    }

    [HttpPost("{id}/credentials/pin")]
    public async Task<IActionResult> SetPin(string id, [FromBody] SetPinRequest request, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(id, ct);
        if (user == null)
        {
            return NotFound(new { error = $"User '{id}' not found" });
        }

        if (string.IsNullOrWhiteSpace(request.Pin))
        {
            return BadRequest(new { error = "PIN cannot be empty" });
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.Pin));
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var credential = new Credential
        {
            UserId = id,
            Type = CredentialType.PIN,
            EncryptedValue = request.Pin,
            HashedValue = hashHex,
            PinLength = request.Pin.Length,
            Label = request.Label ?? "PIN",
            CreatedAt = DateTime.UtcNow
        };

        await _credentialRepo.InsertAsync(credential, ct);
        _logger.LogInformation("Added PIN credential for user '{UserName}' ({UserId})", user.Name, id);

        return Ok(new
        {
            id = credential.Id,
            userId = credential.UserId,
            type = credential.Type.ToString(),
            pinLength = credential.PinLength,
            label = credential.Label,
            createdAt = credential.CreatedAt
        });
    }

    [HttpDelete("{id}/credentials/{credentialId}")]
    public async Task<IActionResult> DeleteCredential(string id, string credentialId, CancellationToken ct)
    {
        await _credentialRepo.DeleteAsync(credentialId, ct);
        return NoContent();
    }

    [HttpGet("{id}/policies")]
    public async Task<IActionResult> GetPolicies(string id, CancellationToken ct)
    {
        var policies = await _policyRepo.GetByUserIdAsync(id, ct);
        return Ok(policies);
    }

    [HttpPost("{id}/policies")]
    public async Task<IActionResult> SavePolicy(string id, [FromBody] AccessPolicy policy, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(id, ct);
        if (user == null)
        {
            return NotFound(new { error = $"User '{id}' not found" });
        }

        if (string.IsNullOrWhiteSpace(policy.Id))
        {
            policy.Id = Guid.NewGuid().ToString();
            await _policyRepo.InsertAsync(policy, ct);
        }
        else
        {
            await _policyRepo.UpdateAsync(policy, ct);
        }

        return Ok(policy);
    }
}
