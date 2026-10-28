using Canteen.Model.DTOs;
using Canteen.Model.Enums;

namespace Canteen.BAL.Interfaces;

public interface ICanteenService
{
    Task<IEnumerable<CanteenDto>> GetAllAsync();
    Task<CanteenDto?> GetByIdAsync(int id);
    Task<CanteenDto> CreateAsync(CreateCanteenDto dto, int? ownerId = null);
    Task<CanteenDto?> UpdateAsync(int id, UpdateCanteenDto dto);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<CanteenSummaryDto>> GetOwnerCanteensAsync(int ownerId);
    Task<bool> IsOwnerAuthorizedAsync(int ownerId, int canteenId);
    Task<bool> IsOwnerAuthorizedAndActiveAsync(int ownerId, int canteenId);
    Task<bool> IsCanteenActiveAsync(int canteenId);
    Task<PlatformAdminMetricsDto> GetPlatformMetricsAsync();
    Task<IEnumerable<AdminCanteenItemDto>> GetAdminCanteensAsync();
    Task<bool> UpdateCanteenStatusAsync(int canteenId, bool isActive);
    Task<bool> UpdateCanteenStatusAsync(int canteenId, bool? isActive, TenantStatus? tenantStatus);
}
