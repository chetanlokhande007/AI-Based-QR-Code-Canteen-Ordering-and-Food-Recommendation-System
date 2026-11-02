using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs;

public class FoodRecommendationDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FoodType { get; set; } = "Veg";
    public string CategoryName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public double RecommendationScore { get; set; }
    public string BadgeText { get; set; } = "Popular Choice";
    public string RecommendationCategory { get; set; } = "Popular";
    public int AvailablePortions { get; set; }
}

public class CartAddonRecommendationDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FoodType { get; set; } = "Veg";
    public string CategoryName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string PairedWithProductName { get; set; } = string.Empty;
    public int PairConfidencePercentage { get; set; }
    public int AvailablePortions { get; set; }
}

public class CartAddonRequestDto
{
    [Required]
    public List<int> CartProductIds { get; set; } = new();

    public string? FoodTypeFilter { get; set; } // "All", "Veg", "Non-Veg"
    public int Limit { get; set; } = 4;
}

public class RecordRecommendationEventDto
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    [RegularExpression("^(Impression|Click|AddToCart|Ordered)$", ErrorMessage = "EventType must be one of: Impression, Click, AddToCart, Ordered.")]
    public string EventType { get; set; } = "Impression";

    [Required]
    [StringLength(50)]
    public string RecommendationCategory { get; set; } = "Popular";

    [StringLength(100)]
    public string? AnonymousSessionId { get; set; }
}

public class TopConvertingDishDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Impressions { get; set; }
    public int Clicks { get; set; }
    public int Orders { get; set; }
    public double ConversionRate { get; set; }
}

public class RecommendationInsightsDto
{
    public int TotalImpressions { get; set; }
    public int TotalClicks { get; set; }
    public int TotalAdditionsToCart { get; set; }
    public int TotalOrders { get; set; }
    public double ClickThroughRate { get; set; }
    public double ConversionRate { get; set; }
    public List<TopConvertingDishDto> TopConvertingDishes { get; set; } = new();
}

