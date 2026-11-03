using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Interfaces;

public interface ICouponService
{
    Task<List<CouponDto>> GetCouponsForCanteenAsync(int canteenId);

    Task<List<PublicCouponDto>> GetActiveCouponsForCustomerAsync(int canteenId);

    Task<CouponDto> GetCouponByIdAsync(int canteenId, int couponId);

    Task<CouponDto> CreateCouponAsync(int canteenId, CreateCouponDto dto);

    Task<CouponDto> UpdateCouponAsync(int canteenId, int couponId, UpdateCouponDto dto);

    Task<bool> ToggleCouponStatusAsync(int canteenId, int couponId);

    Task<bool> DeleteCouponAsync(int canteenId, int couponId);

    Task<ValidateCouponResponseDto> ValidateCouponAsync(int canteenId, ValidateCouponRequestDto request);

    decimal CalculateDiscount(Coupon coupon, decimal foodSubtotal);

    Task<CouponRedemptionReportDto> GetRedemptionReportAsync(int canteenId);
}

