namespace CodeMaster.Core.Models;

public class AccessAssignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AccessPointId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? GroupId { get; set; }
    public string PolicyId { get; set; } = string.Empty;
}
