using System.Security.Cryptography;
using System.Text;
using AccessControl.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccessControl.Engine.Security;

/// <summary>
/// Authenticated credential encryption using AES-256-GCM.
/// Formats ciphertext as Base64([12-byte Nonce][16-byte Tag][Ciphertext]).
/// Supports transparent plaintext fallback for backwards compatibility with pre-encrypted credentials.
/// </summary>
public sealed class AesGcmCredentialEncryptionService : ICredentialEncryptionService, IDisposable
{
    private const int NonceSize = 12; // 96-bit nonce standard for GCM
    private const int TagSize = 16;   // 128-bit authentication tag standard for GCM
    private const int KeySize = 32;   // 256-bit AES key

    private readonly byte[] _key;
    private readonly ILogger<AesGcmCredentialEncryptionService>? _logger;

    public AesGcmCredentialEncryptionService(
        IConfiguration? configuration = null,
        ILogger<AesGcmCredentialEncryptionService>? logger = null)
    {
        _logger = logger;

        var keyConfig = configuration?["Security:EncryptionKey"] 
            ?? configuration?["ENCRYPTION_KEY"];

        if (!string.IsNullOrWhiteSpace(keyConfig))
        {
            if (keyConfig.Length == 64 && keyConfig.All(char.IsAsciiHexDigit))
            {
                _key = Convert.FromHexString(keyConfig);
            }
            else
            {
                try
                {
                    var decoded = Convert.FromBase64String(keyConfig);
                    if (decoded.Length == KeySize)
                    {
                        _key = decoded;
                    }
                    else
                    {
                        _key = SHA256.HashData(decoded);
                    }
                }
                catch
                {
                    _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyConfig));
                }
            }
        }
        else
        {
            // Deterministic default machine-local key fallback for local developer / dev-container environments
            _logger?.LogWarning("Security:EncryptionKey is not configured. Falling back to default system key. Configure a 32-byte key in production.");
            _key = SHA256.HashData(Encoding.UTF8.GetBytes("CodeMaster-Default-AES256-GCM-Key-Salt-2026"));
        }
    }

    /// <summary>
    /// Test-only constructor with explicit 32-byte key.
    /// </summary>
    public AesGcmCredentialEncryptionService(byte[] key, ILogger<AesGcmCredentialEncryptionService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"AES-256 key must be exactly {KeySize} bytes (received {key.Length}).", nameof(key));
        }

        _key = (byte[])key.Clone();
        _logger = logger;
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Combined output: [Nonce (12B)] [Tag (16B)] [Ciphertext (NB)]
        var combined = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, combined, NonceSize + TagSize, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return cipherText;
        }

        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(cipherText);
        }
        catch (FormatException)
        {
            // Legacy plaintext fallback
            return cipherText;
        }

        if (combined.Length < NonceSize + TagSize)
        {
            // Not a valid ciphertext payload; treat as plaintext
            return cipherText;
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipherLength = combined.Length - NonceSize - TagSize;
        var cipherBytes = new byte[cipherLength];
        var plainBytes = new byte[cipherLength];

        Buffer.BlockCopy(combined, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(combined, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(combined, NonceSize + TagSize, cipherBytes, 0, cipherLength);

        try
        {
            using var aesGcm = new AesGcm(_key, TagSize);
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            _logger?.LogDebug("Decryption failed for ciphertext. Falling back to raw string.");
            return cipherText;
        }
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_key);
    }
}
