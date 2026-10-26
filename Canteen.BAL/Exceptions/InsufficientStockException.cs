using System;

namespace Canteen.BAL.Exceptions;

/// <summary>
/// Thrown when an order cannot be fulfilled due to insufficient product stock.
/// </summary>
public class InsufficientStockException : Exception
{
    public int ProductId { get; }
    public string ProductName { get; }

    public InsufficientStockException(string message)
        : base(message)
    {
        ProductName = string.Empty;
    }

    public InsufficientStockException(int productId, string productName, int requestedQuantity, int availableStock)
        : base($"Insufficient stock for '{productName}' (ID: {productId}). Requested: {requestedQuantity}, Available: {availableStock}.")
    {
        ProductId = productId;
        ProductName = productName;
    }
}
