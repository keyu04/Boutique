namespace Boutique.Domain;

public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AdminUser User { get; set; } = default!;

    public string TokenHash { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
}