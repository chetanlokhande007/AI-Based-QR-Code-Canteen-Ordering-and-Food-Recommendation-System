using System;
using System.Collections.Generic;
using System.Text;

namespace Canteen.Model.Entities
{
    public class Canteen
    {
        public int CanteenId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<CanteenTable> Tables { get; set; }
            = new List<CanteenTable>();

        public ICollection<Product> Products { get; set; }
            = new List<Product>();
    }
}
