using System;
using System.Collections.Generic;
using Canteen.Model.Entities;

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

        public decimal TotalAmount { get; set; }

        public string TrackingToken { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Canteen Canteen { get; set; } = null!;

        public CanteenTable Table { get; set; } = null!;

        public ICollection<OrderItem> Items { get; set; }
            = new List<OrderItem>();
    }
}
