using System.Security.Cryptography;
using System.Text;
using AccessControl.Core.DTOs;
using AccessControl.Core.Models;
using AccessControl.Core.Security;
using AccessControl.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace AccessControl.Engine.Services;

public class AccessPolicyEvaluator : IAccessPolicyEvaluator
{
    private readonly IAccessPolicyRepository _policyRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICredentialRepository _credentialRepository;
    private readonly ILogger<AccessPolicyEvaluator>? _logger;

    public AccessPolicyEvaluator(
        IAccessPolicyRepository policyRepository,
        IUserRepository userRepository,
        ICredentialRepository credentialRepository,
        ILogger<AccessPolicyEvaluator>? logger = null)
    {
        _policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _logger = logger;
    }

    public async Task<AccessEvaluationResult> EvaluateAsync(
        KeypadEventDto keypadEvent,
        AccessPoint accessPoint,
        DateTime? evaluationTimeUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keypadEvent.Pin))
        {
            _logger?.LogWarning("Access evaluation failed: PIN was not provided for access point {AccessPointId}", accessPoint.Id);
            return new AccessEvaluationResult(false, null, null, "PIN was not provided");
        }

        var users = await _userRepository.GetAllAsync(cancellationToken);
        User? matchedUser = null;
        Credential? matchedCredential = null;

        foreach (var user in users)
        {
            var credentials = await _credentialRepository.GetByUserIdAsync(user.Id, cancellationToken);
            foreach (var cred in credentials)
            {
                if (VerifyPin(keypadEvent.Pin, cred))
                {
                    matchedUser = user;
                    matchedCredential = cred;
                    break;
                }
            }

            if (matchedUser != null)
            {
                break;
            }
        }

        if (matchedUser == null || matchedCredential == null)
        {
            _logger?.LogWarning("Access denied: Invalid PIN entered for access point {AccessPointId}", accessPoint.Id);
            return new AccessEvaluationResult(false, null, null, "Invalid PIN or credential not found");
        }

        if (!matchedUser.IsActive)
        {
            _logger?.LogWarning("Access denied: User {UserName} ({UserId}) is inactive", matchedUser.Name, matchedUser.Id);
            return new AccessEvaluationResult(false, matchedUser, null, "User is inactive");
        }

        var userPolicies = await _policyRepository.GetByUserIdAsync(matchedUser.Id, cancellationToken);
        var doorPolicies = await _policyRepository.GetByAccessPointIdAsync(accessPoint.Id, cancellationToken);

        IReadOnlyList<AccessPolicy> candidatePolicies;
        if (doorPolicies.Count > 0 && userPolicies.Count > 0)
        {
            var intersection = userPolicies.Where(up => doorPolicies.Any(dp => dp.Id == up.Id)).ToList();
            candidatePolicies = intersection.Count > 0 ? intersection : userPolicies;
        }
        else if (userPolicies.Count > 0)
        {
            candidatePolicies = userPolicies;
        }
        else if (doorPolicies.Count > 0)
        {
            candidatePolicies = doorPolicies;
        }
        else
        {
            _logger?.LogWarning("Access denied: No access policies configured for user {UserName} at {AccessPointName}", matchedUser.Name, accessPoint.Name);
            return new AccessEvaluationResult(false, matchedUser, null, "No access policy found for user at this access point");
        }

        var checkTime = evaluationTimeUtc ?? keypadEvent.Timestamp;
        var activePolicy = candidatePolicies.FirstOrDefault(p => p.IsEnabled && p.IsActiveAt(checkTime));

        if (activePolicy == null)
        {
            var primaryPolicy = candidatePolicies.FirstOrDefault();
            _logger?.LogWarning("Access denied: Policy schedule inactive or expired for user {UserName}", matchedUser.Name);
            return new AccessEvaluationResult(false, matchedUser, primaryPolicy, "Access policy schedule inactive or expired");
        }

        if (activePolicy.ScheduleType == ScheduleType.OneTime)
        {
            activePolicy.RemainingUses = Math.Max(0, (activePolicy.RemainingUses ?? 1) - 1);
            await _policyRepository.UpdateAsync(activePolicy, cancellationToken);
            _logger?.LogInformation("OneTime policy {PolicyName} remaining uses decremented to {RemainingUses}", activePolicy.Name, activePolicy.RemainingUses);
        }

        _logger?.LogInformation("Access granted for user {UserName} under policy {PolicyName}", matchedUser.Name, activePolicy.Name);
        return new AccessEvaluationResult(true, matchedUser, activePolicy, null);
    }

    public static bool VerifyPin(string pin, Credential credential)
    {
        if (string.IsNullOrEmpty(pin))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(credential.HashedValue) && VerifyPinHash(pin, credential.HashedValue))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(credential.EncryptedValue) && string.Equals(pin, credential.EncryptedValue, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    public static bool VerifyPinHash(string pin, string storedHash) =>
        PinSecurityHelper.VerifyPinHash(pin, storedHash);

    public static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string HashPinWithSalt(string pin, string salt)
    {
        var hash = ComputeSha256Hex(salt + pin);
        return $"{salt}:{hash}";
    }
}
