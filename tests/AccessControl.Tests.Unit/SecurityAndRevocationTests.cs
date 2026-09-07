using System.Security.Cryptography;
using System.Text;
using AccessControl.Core.Interfaces;
using AccessControl.Core.Models;
using AccessControl.Core.Security;
using AccessControl.Data.Db;
using AccessControl.Data.Repositories;
using AccessControl.Engine.Security;
using AccessControl.Engine.Services;
using Xunit;

namespace AccessControl.Tests.Unit;

public class SecurityAndRevocationTests
{
    [Fact]
    public void AesGcmEncryption_EncryptAndDecrypt_RoundTripsAccurately()
    {
        using var encryptionService = new AesGcmCredentialEncryptionService();
        const string plainPin = "481516";

        var encrypted = encryptionService.Encrypt(plainPin);

        Assert.NotNull(encrypted);
        Assert.NotEqual(plainPin, encrypted);
        // Base64 check
        var decodedBytes = Convert.FromBase64String(encrypted);
        Assert.True(decodedBytes.Length >= 28); // 12 nonce + 16 tag + ciphertext

        var decrypted = encryptionService.Decrypt(encrypted);
        Assert.Equal(plainPin, decrypted);
    }

    [Fact]
    public void AesGcmEncryption_PlaintextFallback_ReturnsOriginalString()
    {
        using var encryptionService = new AesGcmCredentialEncryptionService();
        const string rawPin = "9876";

        // Plaintext string that is not Base64 encrypted payload
        var result = encryptionService.Decrypt(rawPin);
        Assert.Equal(rawPin, result);
    }

    [Fact]
    public void PinSecurityHelper_CreateSaltedHash_GeneratesSaltAndVerifiesInConstantTime()
    {
        const string pin = "123456";
        var hash1 = PinSecurityHelper.CreateSaltedHash(pin);
        var hash2 = PinSecurityHelper.CreateSaltedHash(pin);

        Assert.Contains(":", hash1);
        Assert.Contains(":", hash2);
        // Salt ensures distinct hashes for identical input
        Assert.NotEqual(hash1, hash2);

        // Constant time verification passes for correct PIN
        Assert.True(PinSecurityHelper.VerifyPinHash(pin, hash1));
        Assert.True(PinSecurityHelper.VerifyPinHash(pin, hash2));

        // Incorrect PIN fails
        Assert.False(PinSecurityHelper.VerifyPinHash("654321", hash1));
        Assert.False(PinSecurityHelper.VerifyPinHash("123455", hash2));
    }

    [Fact]
    public void PinSecurityHelper_LegacyUnsaltedSha256_VerifiesCleanly()
    {
        const string pin = "5544";
        var legacyHex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pin))).ToLowerInvariant();

        Assert.Equal(64, legacyHex.Length);
        Assert.True(PinSecurityHelper.VerifyPinHash(pin, legacyHex));
        Assert.False(PinSecurityHelper.VerifyPinHash("9999", legacyHex));
    }

    [Fact]
    public void AccessPolicy_IsActiveAt_WithTimeZoneId_EvaluatesLocalTimeCorrectly()
    {
        // 9 AM to 5 PM Central Time (UTC-5 during CDT)
        var policy = new AccessPolicy
        {
            ScheduleType = ScheduleType.WeeklyRecurring,
            DaysOfWeek = 127,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            TimeZoneId = "America/Chicago",
            IsEnabled = true
        };

        // 15:00 UTC is 10:00 AM CDT (Active)
        var activeUtc = new DateTime(2026, 9, 7, 15, 0, 0, DateTimeKind.Utc);
        Assert.True(policy.IsActiveAt(activeUtc));

        // 02:00 UTC is 21:00 (9 PM) CDT previous day (Inactive)
        var inactiveUtc = new DateTime(2026, 9, 7, 2, 0, 0, DateTimeKind.Utc);
        Assert.False(policy.IsActiveAt(inactiveUtc));
    }

    [Fact]
    public async Task DoorOperationService_ClearUserHardwareSlots_RevokesSlotOnLockProvider()
    {
        using var connFactory = new SqliteTestConnectionFactory();
        var seeder = new DatabaseSeederService(connFactory, new Microsoft.Extensions.Logging.Abstractions.NullLogger<DatabaseSeederService>());
        await seeder.InitializeAsync();

        var doorRepo = new AccessPointRepository(connFactory);
        var slotRepo = new HardwareSlotRepository(connFactory);
        var userRepo = new UserRepository(connFactory);

        var door = new AccessPoint
        {
            Id = "door-rev-1",
            Name = "Back Door",
            LockProviderType = "GenericMqtt"
        };
        await doorRepo.InsertAsync(door);

        var user = new User
        {
            Id = "user-rev-1",
            Name = "Terminated Contractor",
            Role = UserRole.Service
        };
        await userRepo.InsertAsync(user);

        var credRepo = new CredentialRepository(connFactory);
        var cred = new Credential
        {
            Id = "cred-rev-1",
            UserId = user.Id,
            Type = CredentialType.PIN,
            EncryptedValue = "1234",
            HashedValue = PinSecurityHelper.CreateSaltedHash("1234")
        };
        await credRepo.InsertAsync(cred);

        // Pre-allocate hardware slot 3 to this user
        await slotRepo.AllocateSlotAsync(door.Id, user.Id, cred.Id, 3);

        var doorOps = new DoorOperationService(
            doorRepo,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<DoorOperationService>(),
            mqttClient: null,
            slotRepo: slotRepo);

        var clearedCount = await doorOps.ClearUserHardwareSlotsAsync(user.Id);
        Assert.Equal(1, clearedCount);

        // Verify slot in DB is now empty
        var updatedSlot = await slotRepo.GetSlotAsync(door.Id, 3);
        Assert.NotNull(updatedSlot);
        Assert.Null(updatedSlot!.UserId);
        Assert.Equal(SlotSyncStatus.Synced, updatedSlot.SyncStatus);
    }
}
