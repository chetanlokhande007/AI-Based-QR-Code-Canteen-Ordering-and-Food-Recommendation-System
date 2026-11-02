using System;

namespace Canteen.Model.Entities;

/// <summary>
/// Event log capturing customer interactions with recommended menu dishes (impressions, clicks, add-to-carts, orders).
/// </summary>
public class RecommendationEvent
{
    public int EventId { get; set; }

    public int CanteenId { get; set; }

    public int ProductId { get; set; }

    /// <summary>EventType: Impression, Click, AddToCart, Ordered.</summary>
    public string EventType { get; set; } = "Impression";

    /// <summary>RecommendationCategory: Personalized, Popular, FrequentlyTogether, Similar, TimeBased, Budget.</summary>
    public string RecommendationCategory { get; set; } = "Popular";

    /// <summary>Anonymous pseudo-identifier (e.g. hashed browser session) to measure conversion without collecting PII.</summary>
    public string? AnonymousSessionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Canteen Canteen { get; set; } = null!;

    public Product Product { get; set; } = null!;
}

