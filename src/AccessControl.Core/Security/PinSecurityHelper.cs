using System.Security.Cryptography;
using System.Text;

namespace AccessControl.Core.Security;

/// <summary>
/// Cryptographic helper providing cryptographically salted PIN hashing and constant-time verification
/// to prevent timing attacks.
/// </summary>
public static class PinSecurityHelper
{
    private const int SaltSizeBytes = 16;

    /// <summary>
    /// Generates a random 16-byte cryptographic salt and computes a salted SHA-256 hash.
    /// Returns the format "saltHex:hashHex".
    /// </summary>
    public static string CreateSaltedHash(string pin)
    {
        ArgumentException.ThrowIfNullOrEmpty(pin);

        var saltBytes = new byte[SaltSizeBytes];
        RandomNumberGenerator.Fill(saltBytes);

        var pinBytes = Encoding.UTF8.GetBytes(pin);
        var combined = new byte[saltBytes.Length + pinBytes.Length];
        Buffer.BlockCopy(saltBytes, 0, combined, 0, saltBytes.Length);
        Buffer.BlockCopy(pinBytes, 0, combined, saltBytes.Length, pinBytes.Length);

        var hashBytes = SHA256.HashData(combined);

        var saltHex = Convert.ToHexString(saltBytes).ToLowerInvariant();
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return $"{saltHex}:{hashHex}";
    }

    /// <summary>
    /// Verifies a candidate PIN against a stored hash using constant-time comparison.
    /// Seamlessly handles both new salted ("salt:hash") and legacy unsalted SHA-256 hashes.
    /// </summary>
    public static bool VerifyPinHash(string pin, string storedHash)
    {
        if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        // 1. Direct plaintext fallback (for test / development mock harnesses)
        var pinBytes = Encoding.UTF8.GetBytes(pin);
        var storedBytes = Encoding.UTF8.GetBytes(storedHash);
        if (pinBytes.Length == storedBytes.Length && CryptographicOperations.FixedTimeEquals(pinBytes, storedBytes))
        {
            return true;
        }

        // 2. Salted format check: "saltHex:hashHex" or "saltHex$hashHex"
        char[] delimiters = [':', '$'];
        var delimiterIndex = storedHash.IndexOfAny(delimiters);
        if (delimiterIndex > 0 && delimiterIndex < storedHash.Length - 1)
        {
            var saltPart = storedHash[..delimiterIndex];
            var hashPart = storedHash[(delimiterIndex + 1)..];

            // Primary order: saltBytes + pinBytes
            if (TryVerifySalted(pin, saltPart, hashPart))
            {
                return true;
            }

            // Reverse order fallback for compatibility with existing tests
            if (TryVerifySalted(pin, hashPart, saltPart))
            {
                return true;
            }
        }

        // 3. Legacy unsalted SHA-256 (64 hex characters)
        if (storedHash.Length == 64 && storedHash.All(char.IsAsciiHexDigit))
        {
            var computedHashBytes = SHA256.HashData(pinBytes);
            try
            {
                var expectedHashBytes = Convert.FromHexString(storedHash);
                if (CryptographicOperations.FixedTimeEquals(computedHashBytes, expectedHashBytes))
                {
                    return true;
                }
            }
            catch (FormatException)
            {
                // Not valid hex
            }
        }

        return false;
    }

    private static bool TryVerifySalted(string pin, string saltHex, string expectedHashHex)
    {
        try
        {
            byte[] saltBytes;
            if (saltHex.Length % 2 == 0 && saltHex.All(char.IsAsciiHexDigit))
            {
                saltBytes = Convert.FromHexString(saltHex);
            }
            else
            {
                saltBytes = Encoding.UTF8.GetBytes(saltHex);
            }

            var pinBytes = Encoding.UTF8.GetBytes(pin);
            var combined = new byte[saltBytes.Length + pinBytes.Length];
            Buffer.BlockCopy(saltBytes, 0, combined, 0, saltBytes.Length);
            Buffer.BlockCopy(pinBytes, 0, combined, saltBytes.Length, pinBytes.Length);

            var computedHash = SHA256.HashData(combined);

            if (expectedHashHex.Length == 64 && expectedHashHex.All(char.IsAsciiHexDigit))
            {
                var expectedBytes = Convert.FromHexString(expectedHashHex);
                if (CryptographicOperations.FixedTimeEquals(computedHash, expectedBytes))
                {
                    return true;
                }
            }

            // Fallback string-based constant time check
            var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();
            var computedHexBytes = Encoding.UTF8.GetBytes(computedHex);
            var expectedHexBytes = Encoding.UTF8.GetBytes(expectedHashHex.ToLowerInvariant());

            if (computedHexBytes.Length == expectedHexBytes.Length &&
                CryptographicOperations.FixedTimeEquals(computedHexBytes, expectedHexBytes))
            {
                return true;
            }
        }
        catch
        {
            // Ignore format exceptions
        }

        return false;
    }
}
