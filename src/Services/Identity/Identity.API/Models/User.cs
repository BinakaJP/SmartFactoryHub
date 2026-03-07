namespace Identity.API.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public enum UserRole
{
    /// <summary>Full system access.</summary>
    Admin,
    /// <summary>Can acknowledge alerts, modify thresholds.</summary>
    Engineer,
    /// <summary>Can update equipment status.</summary>
    Operator,
    /// <summary>Read-only access.</summary>
    Viewer
}
