using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IRecommendationRepository
{
    Task<IEnumerable<Order>> GetRecentOrdersWithItemsAsync(int canteenId, int daysBack = 30);

    Task<IEnumerable<Product>> GetCandidateProductsAsync(int canteenId);

    Task<RecommendationEvent> AddEventAsync(RecommendationEvent ev);

    Task<IEnumerable<RecommendationEvent>> GetEventsAsync(int canteenId, int daysBack = 30);
}

