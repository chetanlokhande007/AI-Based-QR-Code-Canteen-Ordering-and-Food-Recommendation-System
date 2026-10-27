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

    // Navigation property for current / active canteen
    public Canteen? Canteen { get; set; }

    // All canteens owned by this owner
    public ICollection<Canteen> Canteens { get; set; } = new List<Canteen>();
}
