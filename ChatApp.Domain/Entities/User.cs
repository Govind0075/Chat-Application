namespace ChatApp.Domain.Entities;

/// <summary>
/// Core user entity.
/// PasswordHash  - BCrypt hash, never store plain text.
/// RefreshToken  - stored in DB for server-side invalidation (token rotation).
/// </summary>
public class User
{
    public Guid     Id                  { get; set; } = Guid.NewGuid();
    public string   Username            { get; set; } = string.Empty;
    public string   Email               { get; set; } = string.Empty;
    public string   PasswordHash        { get; set; } = string.Empty;

    // Refresh-token rotation — a new token replaces the old one on every use
    public string?  RefreshToken        { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    public DateTime CreatedAt           { get; set; } = DateTime.UtcNow;
}
