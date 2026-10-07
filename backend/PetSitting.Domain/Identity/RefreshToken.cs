namespace PetSitting.Domain.Identity;

/// <summary>
/// RefreshToken entity for managing JWT refresh token lifecycle.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }

    // Navigation property
    public User? User { get; set; }
}
