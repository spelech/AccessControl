namespace CodeMaster.Core.Models;

public enum CredentialType
{
    PIN,
    RFID,
    NFC,
    Badge,
    DuressPIN
}

public class Credential
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public CredentialType Type { get; set; } = CredentialType.PIN;
    public string? EncryptedValue { get; set; }
    public string HashedValue { get; set; } = string.Empty;
    public int PinLength { get; set; }
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
