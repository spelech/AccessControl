namespace CodeMaster.Core.Interfaces;

/// <summary>
/// Provides authenticated encryption and decryption for sensitive credential values like PINs and tokens.
/// </summary>
public interface ICredentialEncryptionService
{
    /// <summary>
    /// Encrypts plaintext using AES-256-GCM and returns a Base64-encoded payload containing nonce, tag, and ciphertext.
    /// </summary>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts a Base64-encoded AES-256-GCM ciphertext payload and returns the original plaintext.
    /// Supports graceful plaintext fallback for legacy unencrypted data.
    /// </summary>
    string Decrypt(string cipherText);
}
