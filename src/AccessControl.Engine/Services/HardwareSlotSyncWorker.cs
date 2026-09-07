using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;
using AccessControl.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace AccessControl.Engine.Services;

public class HardwareSlotSyncWorker : IHardwareSlotSyncWorker
{
    private readonly IHardwareSlotRepository _slotRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICredentialRepository _credentialRepository;
    private readonly IAccessPolicyRepository _policyRepository;
    private readonly ICredentialEncryptionService? _encryptionService;
    private readonly ILogger<HardwareSlotSyncWorker>? _logger;

    public HardwareSlotSyncWorker(
        IHardwareSlotRepository slotRepository,
        IUserRepository userRepository,
        ICredentialRepository credentialRepository,
        IAccessPolicyRepository policyRepository,
        ILogger<HardwareSlotSyncWorker>? logger = null,
        ICredentialEncryptionService? encryptionService = null)
    {
        _slotRepository = slotRepository ?? throw new ArgumentNullException(nameof(slotRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
        _logger = logger;
        _encryptionService = encryptionService;
    }

    public async Task ReconcileDoorSlotsAsync(
        AccessPoint accessPoint,
        ILockProvider lockProvider,
        DateTime? checkTimeUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (!lockProvider.Capabilities.HasFlag(LockCapabilities.UserCodes))
        {
            _logger?.LogDebug("Lock provider for access point {AccessPointName} does not support hardware user codes. Skipping slot sync.", accessPoint.Name);
            return;
        }

        var now = checkTimeUtc ?? DateTime.UtcNow;
        var allUsers = await _userRepository.GetAllAsync(cancellationToken);
        var activeUsers = allUsers.Where(u => u.IsActive).ToList();

        var desiredAssignments = new List<(User User, Credential Credential, string PinCode)>();

        foreach (var user in activeUsers)
        {
            var userPolicies = await _policyRepository.GetByUserIdAsync(user.Id, cancellationToken);
            var doorPolicies = await _policyRepository.GetByAccessPointIdAsync(accessPoint.Id, cancellationToken);

            IReadOnlyList<AccessPolicy> policies;
            if (doorPolicies.Count > 0 && userPolicies.Count > 0)
            {
                var intersection = userPolicies.Where(up => doorPolicies.Any(dp => dp.Id == up.Id)).ToList();
                policies = intersection.Count > 0 ? intersection : userPolicies;
            }
            else if (userPolicies.Count > 0)
            {
                policies = userPolicies;
            }
            else
            {
                policies = doorPolicies;
            }

            var hasActivePolicy = policies.Any(p => p.IsEnabled && p.IsActiveAt(now));
            if (!hasActivePolicy)
            {
                continue;
            }

            var credentials = await _credentialRepository.GetByUserIdAsync(user.Id, cancellationToken);
            foreach (var c in credentials.Where(c => c.Type == CredentialType.PIN && !string.IsNullOrWhiteSpace(c.EncryptedValue)))
            {
                var raw = c.EncryptedValue!;
                var pin = _encryptionService != null ? _encryptionService.Decrypt(raw) : raw;
                if (!string.IsNullOrWhiteSpace(pin) && pin.Length >= 4 && pin.Length <= 10 && pin.All(char.IsAsciiDigit))
                {
                    desiredAssignments.Add((user, c, pin));
                    break;
                }
            }
        }

        var currentSlots = (await _slotRepository.GetSlotsForDoorAsync(accessPoint.Id, cancellationToken)).ToList();

        // 1. Remove obsolete or expired user slots
        foreach (var slot in currentSlots.Where(s => !string.IsNullOrEmpty(s.UserId)))
        {
            var stillDesired = desiredAssignments.Any(d => d.User.Id == slot.UserId && d.Credential.Id == slot.CredentialId);
            if (!stillDesired)
            {
                _logger?.LogInformation("Removing expired user {UserId} from slot {SlotNumber} on {AccessPointName}", slot.UserId, slot.SlotNumber, accessPoint.Name);
                await _slotRepository.UpdateSlotSyncStatusAsync(slot.Id, SlotSyncStatus.Deleting, null, cancellationToken);

                var cleared = await lockProvider.ClearSlotCodeAsync(slot.SlotNumber, cancellationToken);
                if (cleared)
                {
                    await _slotRepository.ClearSlotAsync(slot.Id, cancellationToken);
                    await _slotRepository.UpdateSlotSyncStatusAsync(slot.Id, SlotSyncStatus.Synced, DateTime.UtcNow, cancellationToken);
                }
                else
                {
                    _logger?.LogError("Failed to clear hardware slot {SlotNumber} on {AccessPointName}", slot.SlotNumber, accessPoint.Name);
                    await _slotRepository.UpdateSlotSyncStatusAsync(slot.Id, SlotSyncStatus.Error, null, cancellationToken);
                }
            }
        }

        // Refresh slots after deletions
        currentSlots = (await _slotRepository.GetSlotsForDoorAsync(accessPoint.Id, cancellationToken)).ToList();

        // 2. Add or update desired slots
        foreach (var (user, cred, pinCode) in desiredAssignments)
        {

            var existingSlot = currentSlots.FirstOrDefault(s => s.UserId == user.Id && s.CredentialId == cred.Id);

            if (existingSlot != null)
            {
                if (existingSlot.SyncStatus != SlotSyncStatus.Synced)
                {
                    await _slotRepository.UpdateSlotSyncStatusAsync(existingSlot.Id, SlotSyncStatus.Adding, null, cancellationToken);
                    var setOk = await lockProvider.SetSlotCodeAsync(existingSlot.SlotNumber, pinCode, user.Name, cancellationToken);
                    if (setOk)
                    {
                        await _slotRepository.UpdateSlotSyncStatusAsync(existingSlot.Id, SlotSyncStatus.Synced, DateTime.UtcNow, cancellationToken);
                    }
                    else
                    {
                        await _slotRepository.UpdateSlotSyncStatusAsync(existingSlot.Id, SlotSyncStatus.Error, null, cancellationToken);
                    }
                }
                continue;
            }

            // Allocate a slot number between 1 and 30
            var occupiedSlotNumbers = currentSlots.Where(s => !string.IsNullOrEmpty(s.UserId)).Select(s => s.SlotNumber).ToHashSet();
            int? freeSlotNumber = null;
            for (var i = 1; i <= 30; i++)
            {
                if (!occupiedSlotNumbers.Contains(i))
                {
                    freeSlotNumber = i;
                    break;
                }
            }

            if (freeSlotNumber == null)
            {
                _logger?.LogError("No hardware slots available (1-30 full) for user {UserName} on {AccessPointName}", user.Name, accessPoint.Name);
                continue;
            }

            var allocatedSlot = await _slotRepository.AllocateSlotAsync(accessPoint.Id, user.Id, cred.Id, freeSlotNumber.Value, cancellationToken);
            if (allocatedSlot != null)
            {
                await _slotRepository.UpdateSlotSyncStatusAsync(allocatedSlot.Id, SlotSyncStatus.Adding, null, cancellationToken);
                var setOk = await lockProvider.SetSlotCodeAsync(freeSlotNumber.Value, pinCode, user.Name, cancellationToken);
                if (setOk)
                {
                    await _slotRepository.UpdateSlotSyncStatusAsync(allocatedSlot.Id, SlotSyncStatus.Synced, DateTime.UtcNow, cancellationToken);
                    currentSlots.Add(allocatedSlot);
                }
                else
                {
                    await _slotRepository.UpdateSlotSyncStatusAsync(allocatedSlot.Id, SlotSyncStatus.Error, null, cancellationToken);
                }
            }
        }
    }
}
