using System;

namespace Canteen.Model.Entities
{
    public class CanteenTable
    {
        public int TableId { get; set; }

        public int CanteenId { get; set; }

        public int TableNumber { get; set; }

        public int Capacity { get; set; }

        /// <summary>Unique QR code token used for table-side ordering.</summary>
        public string QRToken { get; set; } = Guid.NewGuid().ToString("N");

        public bool IsAvailable { get; set; } = true;

        // Navigation
        public Canteen Canteen { get; set; } = null!;
    }
}
