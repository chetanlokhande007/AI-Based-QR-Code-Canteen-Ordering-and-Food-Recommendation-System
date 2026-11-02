using System;
using System.Collections.Generic;

namespace Canteen.Model.Entities
{
    public class Product
    {
        public int ProductId { get; set; }

        public int CanteenId { get; set; }

        public int CategoryId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int StockQuantity { get; set; }

        public bool IsAvailable { get; set; } = true;

        public string FoodType { get; set; } = "Veg";

        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Canteen Canteen { get; set; } = null!;

        public Category Category { get; set; } = null!;

        public Recipe? Recipe { get; set; }

        public ICollection<RecommendationEvent> RecommendationEvents { get; set; } = new List<RecommendationEvent>();
    }
}
