using System;

namespace Canteen.Model.Entities;

public class TvPairingSession
{
    public int SessionId { get; set; }

    public int CanteenId { get; set; }

    public string PairingCode { get; set; } = string.Empty;

    public string StationScope { get; set; } = "All";

    public int AttemptCount { get; set; } = 0;

    public bool IsClaimed { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);

    // Navigation
    public Canteen Canteen { get; set; } = null!;
}

