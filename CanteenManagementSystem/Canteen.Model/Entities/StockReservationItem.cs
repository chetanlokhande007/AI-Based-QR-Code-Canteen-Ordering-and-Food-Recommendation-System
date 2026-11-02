namespace Canteen.Model.Entities;

/// <summary>
/// Specific ingredient quantity locked inside a StockReservation.
/// </summary>
public class StockReservationItem
{
    public int ReservationItemId { get; set; }

    public int ReservationId { get; set; }

    public int IngredientId { get; set; }

    public decimal ReservedQuantity { get; set; }

    // Navigation properties
    public StockReservation Reservation { get; set; } = null!;

    public InventoryIngredient Ingredient { get; set; } = null!;
}

