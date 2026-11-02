using System;
using System.Collections.Generic;

namespace Canteen.Model.Entities;

/// <summary>
/// Locks raw ingredient quantities for a placed customer order until preparation begins or order is cancelled.
/// </summary>
public class StockReservation
{
    public int ReservationId { get; set; }

    public int OrderId { get; set; }

    public int CanteenId { get; set; }

    /// <summary>Status: Active, Consumed, Released.</summary>
    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ConsumedAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    // Navigation properties
    public Order Order { get; set; } = null!;

    public Canteen Canteen { get; set; } = null!;

    public ICollection<StockReservationItem> Items { get; set; } = new List<StockReservationItem>();
}

