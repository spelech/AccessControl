namespace CodeMaster.Core.Models;

public enum UserRole
{
    Admin,
    Member,
    Guest,
    Service
}

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Member;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
