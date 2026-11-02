using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IFoodRecommendationService
{
    Task<IEnumerable<FoodRecommendationDto>> GetMenuRecommendationsAsync(
        int canteenId,
        string? foodType = null,
        int limit = 6,
        DateTime? simulatedTimeUtc = null);

    Task<IEnumerable<CartAddonRecommendationDto>> GetCartAddonsAsync(
        int canteenId,
        CartAddonRequestDto request);

    Task<bool> RecordEventAsync(int canteenId, RecordRecommendationEventDto dto);

    Task<RecommendationInsightsDto> GetInsightsAsync(int canteenId, int daysBack = 30);
}

