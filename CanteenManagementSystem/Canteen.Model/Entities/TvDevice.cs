using System;

namespace Canteen.Model.Entities;

public class TvDevice
{
    public int TvDeviceId { get; set; }

    public int CanteenId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceIdentifier { get; set; } = string.Empty;

    public string DeviceTokenHash { get; set; } = string.Empty;

    public string StationScope { get; set; } = "All"; // "All", "Food", "Beverage"

    public bool IsActive { get; set; } = true;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastConnectedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    // Navigation
    public Canteen Canteen { get; set; } = null!;
}

