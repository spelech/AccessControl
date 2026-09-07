using AccessControl.Core.DTOs;
using AccessControl.Core.Models;
using AccessControl.Data.Repositories;
using AccessControl.Engine.Services;
using NSubstitute;
using Xunit;

namespace AccessControl.Tests.Unit;

public class AccessPolicyEvaluatorTests
{
    private readonly IAccessPolicyRepository _policyRepo;
    private readonly IUserRepository _userRepo;
    private readonly ICredentialRepository _credentialRepo;
    private readonly AccessPolicyEvaluator _evaluator;

    public AccessPolicyEvaluatorTests()
    {
        _policyRepo = Substitute.For<IAccessPolicyRepository>();
        _userRepo = Substitute.For<IUserRepository>();
        _credentialRepo = Substitute.For<ICredentialRepository>();
        _evaluator = new AccessPolicyEvaluator(_policyRepo, _userRepo, _credentialRepo);
    }

    [Fact]
    public async Task EvaluateAsync_ValidPin_WithinWeeklySchedule_Succeeds()
    {
        var user = new User { Id = "user-1", Name = "Alice", IsActive = true };
        var pin = "1234";
        var cred = new Credential
        {
            Id = "cred-1",
            UserId = user.Id,
            Type = CredentialType.PIN,
            HashedValue = AccessPolicyEvaluator.ComputeSha256Hex(pin)
        };
        var policy = new AccessPolicy
        {
            Id = "policy-1",
            Name = "Workday Access",
            ScheduleType = ScheduleType.WeeklyRecurring,
            DaysOfWeek = 127, // All days
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(18, 0),
            IsEnabled = true
        };
        var door = new AccessPoint { Id = "door-1", Name = "Front Door" };

        _userRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([user]);
        _credentialRepo.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns([cred]);
        _policyRepo.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns([policy]);
        _policyRepo.GetByAccessPointIdAsync(door.Id, Arg.Any<CancellationToken>()).Returns([policy]);

        var testTime = new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc); // Monday 10:00
        var evt = new KeypadEventDto { Pin = pin, Timestamp = testTime };

        var result = await _evaluator.EvaluateAsync(evt, door, testTime);

        Assert.True(result.IsValid);
        Assert.NotNull(result.User);
        Assert.Equal("Alice", result.User.Name);
        Assert.NotNull(result.Policy);
        Assert.Equal("Workday Access", result.Policy.Name);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_ExpiredDateRange_Fails()
    {
        var user = new User { Id = "user-guest", Name = "Bob", IsActive = true };
        var pin = "5555";
        var cred = new Credential
        {
            Id = "cred-guest",
            UserId = user.Id,
            Type = CredentialType.PIN,
            HashedValue = AccessPolicyEvaluator.ComputeSha256Hex(pin)
        };
        var policy = new AccessPolicy
        {
            Id = "policy-guest",
            Name = "Guest Weekend",
            ScheduleType = ScheduleType.DateRange,
            ValidFrom = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ValidUntil = new DateTime(2026, 9, 3, 23, 59, 59, DateTimeKind.Utc),
            IsEnabled = true
        };
        var door = new AccessPoint { Id = "door-1", Name = "Front Door" };

        _userRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([user]);
        _credentialRepo.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns([cred]);
        _policyRepo.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns([policy]);
        _policyRepo.GetByAccessPointIdAsync(door.Id, Arg.Any<CancellationToken>()).Returns([policy]);

        // Attempt on Sep 5 (after ValidUntil)
        var attemptTime = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
        var evt = new KeypadEventDto { Pin = pin, Timestamp = attemptTime };

        var result = await _evaluator.EvaluateAsync(evt, door, attemptTime);

        Assert.False(result.IsValid);
        Assert.NotNull(result.User);
        Assert.NotNull(result.Reason);
        Assert.Contains("expired", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_InvalidPin_FailsAndLogsReason()
    {
        var user = new User { Id = "user-1", Name = "Alice", IsActive = true };
        var cred = new Credential
        {
            Id = "cred-1",
            UserId = user.Id,
            Type = CredentialType.PIN,
            HashedValue = AccessPolicyEvaluator.ComputeSha256Hex("9999")
        };
        var door = new AccessPoint { Id = "door-1", Name = "Front Door" };

        _userRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([user]);
        _credentialRepo.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns([cred]);

        var evt = new KeypadEventDto { Pin = "0000" };

        var result = await _evaluator.EvaluateAsync(evt, door);

        Assert.False(result.IsValid);
        Assert.Null(result.User);
        Assert.NotNull(result.Reason);
        Assert.Contains("Invalid PIN", result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_OneTimePolicy_DecrementsRemainingUses()
    {
        var user = new User { Id = "user-contractor", Name = "Dave", IsActive = true };
        var pin = "7890";
        var cred = new Credential
        {
            Id = "cred-contractor",
            UserId = user.Id,
            Type = CredentialType.PIN,
            HashedValue = AccessPolicyEvaluator.ComputeSha256Hex(pin)
        };
        var policy = new AccessPolicy
        {
            Id = "policy-onetime",
            Name = "Delivery Once",
            ScheduleType = ScheduleType.OneTime,
            RemainingUses = 1,
            IsEnabled = true
        };
        var door = new AccessPoint { Id = "door-1", Name = "Front Door" };

        _userRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([user]);
        _credentialRepo.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns([cred]);
        _policyRepo.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns([policy]);
        _policyRepo.GetByAccessPointIdAsync(door.Id, Arg.Any<CancellationToken>()).Returns([policy]);

        var evt = new KeypadEventDto { Pin = pin };

        var result = await _evaluator.EvaluateAsync(evt, door);

        Assert.True(result.IsValid);
        Assert.Equal(0, policy.RemainingUses);
        await _policyRepo.Received(1).UpdateAsync(Arg.Is<AccessPolicy>(p => p.Id == policy.Id && p.RemainingUses == 0), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_SaltedHashPin_MatchesSuccessfully()
    {
        var user = new User { Id = "user-salted", Name = "Eve", IsActive = true };
        var pin = "4321";
        var salt = "random_salt_xyz";
        var cred = new Credential
        {
            Id = "cred-salted",
            UserId = user.Id,
            Type = CredentialType.PIN,
            HashedValue = AccessPolicyEvaluator.HashPinWithSalt(pin, salt)
        };
        var policy = new AccessPolicy
        {
            Id = "policy-always",
            Name = "Always Open",
            ScheduleType = ScheduleType.Always,
            IsEnabled = true
        };
        var door = new AccessPoint { Id = "door-1", Name = "Front Door" };

        _userRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([user]);
        _credentialRepo.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns([cred]);
        _policyRepo.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns([policy]);

        var evt = new KeypadEventDto { Pin = pin };

        var result = await _evaluator.EvaluateAsync(evt, door);

        Assert.True(result.IsValid);
        Assert.NotNull(result.User);
        Assert.Equal("Eve", result.User.Name);
    }
}
