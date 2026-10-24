using System.Collections.Generic;

namespace Canteen.Model.Entities
{
    public class Category
    {
        public int CategoryId { get; set; }

        public int CanteenId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // Navigation
        public Canteen Canteen { get; set; } = null!;

        public ICollection<Product> Products { get; set; }
            = new List<Product>();
    }
}
