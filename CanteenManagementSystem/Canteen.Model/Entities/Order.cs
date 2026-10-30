using System;
using System.Collections.Generic;

namespace Canteen.Model.Entities
{
    public class Order
    {
        public int OrderId { get; set; }

        public int CanteenId { get; set; }

        public int TableId { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";

        public string PaymentStatus { get; set; } = "Pending";

        // Food Subtotal (Sum of items * quantity)
        public decimal SubtotalAmount { get; set; }

        // Tax / GST amount
        public decimal TaxAmount { get; set; }

        // Marketplace Platform Fee (e.g. ₹5.00)
        public decimal PlatformFeeAmount { get; set; } = 5.00m;

        // Customer Pays or Canteen Absorbs
        public string PlatformFeePolicy { get; set; } = "CustomerPays";

        // Total amount paid by the customer (Subtotal + Tax + (Policy == CustomerPays ? Fee : 0))
        public decimal TotalAmount { get; set; }

        // Net amount payable to Canteen Owner (Subtotal + Tax - (Policy == CanteenAbsorbs ? Fee : 0))
        public decimal CanteenEarningsAmount { get; set; }

        public string TrackingToken { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Canteen Canteen { get; set; } = null!;

        public CanteenTable Table { get; set; } = null!;

        public ICollection<OrderItem> Items { get; set; }
            = new List<OrderItem>();
    }
}
