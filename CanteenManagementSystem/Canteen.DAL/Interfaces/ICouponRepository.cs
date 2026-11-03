using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface ICouponRepository
{
    Task<Coupon?> GetByIdAsync(int couponId);

    /// <summary>
    /// Looks up a coupon by code. Prioritizes canteen-specific coupons, and falls back to platform-wide coupons (CanteenId == null).
    /// </summary>
    Task<Coupon?> GetByCodeAsync(int? canteenId, string code);

    Task<List<Coupon>> GetActiveCouponsForCanteenAsync(int canteenId);

    Task<List<Coupon>> GetAllCouponsForCanteenAsync(int canteenId);

    Task<List<Coupon>> GetPlatformCouponsAsync();

    Task<Coupon> AddCouponAsync(Coupon coupon);

    Task<Coupon> UpdateCouponAsync(Coupon coupon);

    Task<bool> DeleteCouponAsync(int couponId);

    Task<CouponRedemption> AddRedemptionAsync(CouponRedemption redemption);

    Task<int> GetCustomerRedemptionCountAsync(int couponId, string customerIdentifier);

    Task<CouponRedemptionReportDto> GetRedemptionReportForCanteenAsync(int canteenId);
}

