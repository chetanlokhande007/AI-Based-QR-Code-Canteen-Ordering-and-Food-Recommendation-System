using System;

namespace Canteen.Model.Entities;

public class Owner
{
    public int OwnerId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "CanteenOwner";

    public int? CanteenId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetTokenExpiry { get; set; }

    // Navigation property
    public Canteen? Canteen { get; set; }
}
