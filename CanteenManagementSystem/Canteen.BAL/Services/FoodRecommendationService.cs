using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Services;

public class FoodRecommendationService : IFoodRecommendationService
{
    private readonly IRecommendationRepository _recommendationRepo;

    public FoodRecommendationService(IRecommendationRepository recommendationRepo)
    {
        _recommendationRepo = recommendationRepo;
    }

    public async Task<IEnumerable<FoodRecommendationDto>> GetMenuRecommendationsAsync(
        int canteenId,
        string? foodType = null,
        int limit = 6,
        DateTime? simulatedTimeUtc = null)
    {
        var candidateProducts = (await _recommendationRepo.GetCandidateProductsAsync(canteenId)).ToList();
        if (!candidateProducts.Any())
        {
            return Enumerable.Empty<FoodRecommendationDto>();
        }

        // Apply food type filter (Veg / Non-Veg) if requested
        if (!string.IsNullOrWhiteSpace(foodType) && !string.Equals(foodType, "All", StringComparison.OrdinalIgnoreCase))
        {
            candidateProducts = candidateProducts
                .Where(p => string.Equals(p.FoodType, foodType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Fetch recent orders for popularity and time-of-day analytics
        var recentOrders = (await _recommendationRepo.GetRecentOrdersWithItemsAsync(canteenId, daysBack: 30)).ToList();
        int totalOrdersCount = recentOrders.Count;

        // Determine current local meal period (IST = UTC+5:30)
        var currentTime = (simulatedTimeUtc ?? DateTime.UtcNow).AddHours(5.5);
        double hourOfDay = currentTime.Hour + (currentTime.Minute / 60.0);
        string currentMealPeriod = GetMealPeriod(hourOfDay);

        // Filter recent orders belonging to the current meal period
        var periodOrders = recentOrders
            .Where(o => GetMealPeriod((o.CreatedAt.AddHours(5.5).Hour) + (o.CreatedAt.AddHours(5.5).Minute / 60.0)) == currentMealPeriod)
            .ToList();

        // Count dish occurrences in overall orders and current period orders
        var overallOrderCounts = new Dictionary<int, int>();
        foreach (var order in recentOrders)
        {
            foreach (var item in order.Items.Select(i => i.ProductId).Distinct())
            {
                overallOrderCounts[item] = overallOrderCounts.GetValueOrDefault(item, 0) + 1;
            }
        }

        var periodOrderCounts = new Dictionary<int, int>();
        foreach (var order in periodOrders)
        {
            foreach (var item in order.Items.Select(i => i.ProductId).Distinct())
            {
                periodOrderCounts[item] = periodOrderCounts.GetValueOrDefault(item, 0) + 1;
            }
        }

        int maxOverallOrders = overallOrderCounts.Values.DefaultIfEmpty(1).Max();
        int maxPeriodOrders = periodOrderCounts.Values.DefaultIfEmpty(1).Max();

        var recommendations = new List<FoodRecommendationDto>();

        foreach (var product in candidateProducts)
        {
            // 1. Calculate live portion availability
            int availablePortions = CalculatePortionAvailability(product);
            if (availablePortions <= 0)
            {
                // Suppress dishes that cannot be prepared by the kitchen
                continue;
            }

            // 2. Heuristic 1: Overall Popularity Score [0.0 - 1.0]
            int productOverallCount = overallOrderCounts.GetValueOrDefault(product.ProductId, 0);
            double popularityScore = maxOverallOrders > 0 ? (double)productOverallCount / maxOverallOrders : 0.0;

            // 3. Heuristic 2: Time-of-Day Score [0.0 - 1.0]
            int productPeriodCount = periodOrderCounts.GetValueOrDefault(product.ProductId, 0);
            double historicalTimeScore = maxPeriodOrders > 0 ? (double)productPeriodCount / maxPeriodOrders : 0.0;
            double semanticTimeBoost = CalculateSemanticTimeBoost(product, currentMealPeriod);
            double timeScore = periodOrders.Any()
                ? Math.Min(1.0, (historicalTimeScore * 0.6) + (semanticTimeBoost * 0.4))
                : semanticTimeBoost;

            // 4. Heuristic 3: Value Score (healthy margin & price fairness)
            double valueScore = product.Price > 0 ? Math.Min(1.0, 100.0 / (double)product.Price) : 0.5;

            // 5. Blended Score
            // Weights: Time-of-Day (50%), Popularity (35%), Value (15%)
            double finalScore = Math.Round(
                (timeScore * 0.50) +
                (popularityScore * 0.35) +
                (valueScore * 0.15),
                3);

            // Determine customer-facing badge
            string badgeText;
            string categoryTag;

            if (timeScore >= 0.5)
            {
                badgeText = currentMealPeriod switch
                {
                    "Breakfast" => "Trending Breakfast",
                    "Lunch" => "Lunch Favorite",
                    "Snacks" => "Evening Bite",
                    "Dinner" => "Dinner Special",
                    _ => "Chef's Special"
                };
                categoryTag = "TimeBased";
            }
            else if (popularityScore >= 0.5)
            {
                badgeText = "Canteen Bestseller";
                categoryTag = "Popular";
            }
            else if (product.Price <= 60 && product.Price > 0)
            {
                badgeText = "Great Value";
                categoryTag = "ValuePick";
            }
            else
            {
                badgeText = "Chef's Recommendation";
                categoryTag = "Popular";
            }

            recommendations.Add(new FoodRecommendationDto
            {
                ProductId = product.ProductId,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                FoodType = product.FoodType,
                CategoryName = product.Category?.Name ?? "General",
                ImageUrl = product.ImageUrl,
                RecommendationScore = finalScore,
                BadgeText = badgeText,
                RecommendationCategory = categoryTag,
                AvailablePortions = availablePortions
            });
        }

        return recommendations
            .OrderByDescending(r => r.RecommendationScore)
            .Take(Math.Max(1, limit))
            .ToList();
    }

    public async Task<IEnumerable<CartAddonRecommendationDto>> GetCartAddonsAsync(
        int canteenId,
        CartAddonRequestDto request)
    {
        if (request == null || request.CartProductIds == null || !request.CartProductIds.Any())
        {
            return Enumerable.Empty<CartAddonRecommendationDto>();
        }

        var cartIds = new HashSet<int>(request.CartProductIds);
        var candidateProducts = (await _recommendationRepo.GetCandidateProductsAsync(canteenId))
            .Where(p => !cartIds.Contains(p.ProductId))
            .ToList();

        if (!candidateProducts.Any())
        {
            return Enumerable.Empty<CartAddonRecommendationDto>();
        }

        if (!string.IsNullOrWhiteSpace(request.FoodTypeFilter) && !string.Equals(request.FoodTypeFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            candidateProducts = candidateProducts
                .Where(p => string.Equals(p.FoodType, request.FoodTypeFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var recentOrders = (await _recommendationRepo.GetRecentOrdersWithItemsAsync(canteenId, daysBack: 60)).ToList();

        // 1. Basket Co-occurrence Mining:
        // Find orders containing any of the cart products
        var relevantOrders = recentOrders
            .Where(o => o.Items.Any(i => cartIds.Contains(i.ProductId)))
            .ToList();

        var pairCounts = new Dictionary<int, (int Count, int PairedWithProductId)>();

        foreach (var order in relevantOrders)
        {
            var cartItemsInThisOrder = order.Items.Where(i => cartIds.Contains(i.ProductId)).Select(i => i.ProductId).ToList();
            var otherItemsInThisOrder = order.Items.Where(i => !cartIds.Contains(i.ProductId)).Select(i => i.ProductId).Distinct().ToList();

            foreach (var otherId in otherItemsInThisOrder)
            {
                var pairedWith = cartItemsInThisOrder.First();
                if (pairCounts.TryGetValue(otherId, out var existing))
                {
                    pairCounts[otherId] = (existing.Count + 1, pairedWith);
                }
                else
                {
                    pairCounts[otherId] = (1, pairedWith);
                }
            }
        }

        var addons = new List<CartAddonRecommendationDto>();
        var candidateMap = candidateProducts.ToDictionary(p => p.ProductId);
        var cartProductNames = (await _recommendationRepo.GetCandidateProductsAsync(canteenId))
            .Where(p => cartIds.Contains(p.ProductId))
            .ToDictionary(p => p.ProductId, p => p.Name);

        // Map co-occurrence results
        foreach (var (productId, (count, pairedWithId)) in pairCounts.OrderByDescending(p => p.Value.Count))
        {
            if (candidateMap.TryGetValue(productId, out var product))
            {
                int portions = CalculatePortionAvailability(product);
                if (portions <= 0) continue;

                int confidence = relevantOrders.Count > 0
                    ? Math.Min(98, Math.Max(50, (int)Math.Round(((double)count / relevantOrders.Count) * 100)))
                    : 65;

                addons.Add(new CartAddonRecommendationDto
                {
                    ProductId = product.ProductId,
                    Name = product.Name,
                    Price = product.Price,
                    FoodType = product.FoodType,
                    CategoryName = product.Category?.Name ?? "General",
                    ImageUrl = product.ImageUrl,
                    PairedWithProductName = cartProductNames.GetValueOrDefault(pairedWithId, "Your Cart"),
                    PairConfidencePercentage = confidence,
                    AvailablePortions = portions
                });
            }
        }

        // 2. Cold-Start Complementary Heuristic (If basket history has few or no matches)
        if (addons.Count < request.Limit)
        {
            // Beverage / snack cross-sell
            var complementaryProducts = candidateProducts
                .Where(p => !addons.Any(a => a.ProductId == p.ProductId))
                .OrderBy(p => IsBeverageOrSnack(p) ? 0 : 1)
                .ThenBy(p => p.Price)
                .ToList();

            string primaryCartName = cartProductNames.Values.FirstOrDefault() ?? "Your Order";

            foreach (var comp in complementaryProducts)
            {
                if (addons.Count >= request.Limit) break;

                int portions = CalculatePortionAvailability(comp);
                if (portions <= 0) continue;

                addons.Add(new CartAddonRecommendationDto
                {
                    ProductId = comp.ProductId,
                    Name = comp.Name,
                    Price = comp.Price,
                    FoodType = comp.FoodType,
                    CategoryName = comp.Category?.Name ?? "General",
                    ImageUrl = comp.ImageUrl,
                    PairedWithProductName = primaryCartName,
                    PairConfidencePercentage = 75,
                    AvailablePortions = portions
                });
            }
        }

        return addons.Take(Math.Max(1, request.Limit)).ToList();
    }

    public async Task<bool> RecordEventAsync(int canteenId, RecordRecommendationEventDto dto)
    {
        if (dto == null) return false;

        var ev = new RecommendationEvent
        {
            CanteenId = canteenId,
            ProductId = dto.ProductId,
            EventType = dto.EventType,
            RecommendationCategory = dto.RecommendationCategory,
            AnonymousSessionId = dto.AnonymousSessionId?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _recommendationRepo.AddEventAsync(ev);
        return true;
    }

    public async Task<RecommendationInsightsDto> GetInsightsAsync(int canteenId, int daysBack = 30)
    {
        var events = (await _recommendationRepo.GetEventsAsync(canteenId, daysBack)).ToList();

        int impressions = events.Count(e => e.EventType == "Impression");
        int clicks = events.Count(e => e.EventType == "Click");
        int addToCarts = events.Count(e => e.EventType == "AddToCart");
        int orders = events.Count(e => e.EventType == "Ordered");

        double ctr = impressions > 0 ? Math.Round(((double)clicks / impressions) * 100, 2) : 0.0;
        double conversionRate = clicks > 0 ? Math.Round(((double)orders / clicks) * 100, 2) : 0.0;

        var dishGroups = events
            .GroupBy(e => new { e.ProductId, ProductName = e.Product?.Name ?? $"Dish #{e.ProductId}" })
            .Select(g =>
            {
                int pImpressions = g.Count(e => e.EventType == "Impression");
                int pClicks = g.Count(e => e.EventType == "Click");
                int pOrders = g.Count(e => e.EventType == "Ordered");
                double pConv = pClicks > 0 ? Math.Round(((double)pOrders / pClicks) * 100, 2) : 0.0;

                return new TopConvertingDishDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    Impressions = pImpressions,
                    Clicks = pClicks,
                    Orders = pOrders,
                    ConversionRate = pConv
                };
            })
            .OrderByDescending(d => d.Orders)
            .ThenByDescending(d => d.ConversionRate)
            .Take(10)
            .ToList();

        return new RecommendationInsightsDto
        {
            TotalImpressions = impressions,
            TotalClicks = clicks,
            TotalAdditionsToCart = addToCarts,
            TotalOrders = orders,
            ClickThroughRate = ctr,
            ConversionRate = conversionRate,
            TopConvertingDishes = dishGroups
        };
    }

    // =========================================================================
    // Private Scoring Helpers
    // =========================================================================

    private static string GetMealPeriod(double hour)
    {
        if (hour >= 6.0 && hour < 11.5) return "Breakfast";
        if (hour >= 11.5 && hour < 15.5) return "Lunch";
        if (hour >= 15.5 && hour < 19.0) return "Snacks";
        if (hour >= 19.0 && hour < 23.5) return "Dinner";
        return "LateNight";
    }

    private static double CalculateSemanticTimeBoost(Product product, string mealPeriod)
    {
        var cat = product.Category?.Name?.ToLowerInvariant() ?? "";
        var text = $"{product.Name} {product.Description}".ToLowerInvariant();

        if (mealPeriod == "Breakfast")
        {
            if (cat.Contains("breakfast")) return 0.95;
            if (text.Contains("poha") || text.Contains("idli") || text.Contains("dosa") ||
                text.Contains("upma") || text.Contains("tea") || text.Contains("chai") ||
                text.Contains("coffee") || text.Contains("paratha") || text.Contains("breakfast")) return 0.9;
            if (cat.Contains("main") || cat.Contains("curry") || cat.Contains("thali")) return 0.1;
            return 0.3;
        }

        if (mealPeriod == "Lunch")
        {
            if (cat.Contains("breakfast")) return 0.1;
            if (cat.Contains("main") || cat.Contains("thali") || cat.Contains("meal")) return 0.95;
            if (text.Contains("thali") || text.Contains("biryani") || text.Contains("curry") ||
                text.Contains("roti") || text.Contains("paneer") || text.Contains("lunch") ||
                text.Contains("fried rice") || text.Contains("steamed rice") || text.Contains("jeera rice")) return 0.9;
            return 0.25;
        }

        if (mealPeriod == "Snacks")
        {
            if (cat.Contains("snack") || cat.Contains("fast food") || cat.Contains("beverage")) return 0.95;
            if (text.Contains("sandwich") || text.Contains("samosa") || text.Contains("snack") ||
                text.Contains("burger") || text.Contains("pizza") || text.Contains("chai") ||
                text.Contains("tea") || text.Contains("coffee") || text.Contains("fries")) return 0.9;
            return 0.25;
        }

        if (mealPeriod == "Dinner")
        {
            if (cat.Contains("breakfast")) return 0.1;
            if (cat.Contains("main") || cat.Contains("curry") || cat.Contains("dinner")) return 0.95;
            if (text.Contains("curry") || text.Contains("paneer") || text.Contains("biryani") ||
                text.Contains("roti") || text.Contains("naan") || text.Contains("dal") ||
                text.Contains("dinner") || text.Contains("chicken")) return 0.9;
            return 0.25;
        }

        return 0.5;
    }

    private static bool IsBeverageOrSnack(Product product)
    {
        var cat = product.Category?.Name?.ToLowerInvariant() ?? "";
        var name = product.Name.ToLowerInvariant();
        return cat.Contains("beverage") || cat.Contains("drink") || cat.Contains("snack") ||
               name.Contains("coffee") || name.Contains("tea") || name.Contains("chai") ||
               name.Contains("samosa") || name.Contains("juice");
    }

    private static int CalculatePortionAvailability(Product product)
    {
        if (product.Recipe != null && product.Recipe.IsActive && product.Recipe.Ingredients.Any())
        {
            int minPortions = int.MaxValue;
            var servingSize = Math.Max(1, product.Recipe.ServingSize);

            foreach (var ri in product.Recipe.Ingredients)
            {
                var ingredient = ri.Ingredient;
                if (ingredient == null) continue;

                decimal multiplier = 1.0m;
                try
                {
                    multiplier = RecipeService.GetUnitConversionMultiplier(ri.Unit, ingredient.BaseUnit, ingredient.Name);
                }
                catch
                {
                    multiplier = 1.0m;
                }

                var normalizedRequired = (ri.Quantity * multiplier) / servingSize;
                int portionsPossible = normalizedRequired > 0
                    ? (int)Math.Floor(ingredient.AvailableStock / normalizedRequired)
                    : 999999;

                if (portionsPossible < minPortions)
                {
                    minPortions = portionsPossible;
                }
            }

            return minPortions == int.MaxValue ? 0 : Math.Max(0, minPortions);
        }

        return product.StockQuantity;
    }
}
