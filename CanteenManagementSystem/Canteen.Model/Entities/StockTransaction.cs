using System;

namespace Canteen.Model.Entities;

/// <summary>
/// Append-only auditable ledger recording all stock movements, deductions, additions, and reservations.
/// </summary>
public class StockTransaction
{
    public int TransactionId { get; set; }

    public int CanteenId { get; set; }

    public int IngredientId { get; set; }

    /// <summary>
    /// Movement category: OpeningStock, Purchase, Adjustment, Reservation, Consumption, ReservationRelease, Wastage, RefundRestock.
    /// </summary>
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>Quantity changed in base unit (g, ml, pcs).</summary>
    public decimal Quantity { get; set; }

    public decimal PreviousStock { get; set; }

    public decimal NewStock { get; set; }

    public int? OrderId { get; set; }

    public string? ReferenceNumber { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int? CreatedByOwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Canteen Canteen { get; set; } = null!;

    public InventoryIngredient Ingredient { get; set; } = null!;

    public Order? Order { get; set; }
}

