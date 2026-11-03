using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.BAL.Services;

public class CouponService : ICouponService
{
    private readonly ICouponRepository _couponRepository;

    public CouponService(ICouponRepository couponRepository)
    {
        _couponRepository = couponRepository;
    }

    public async Task<List<CouponDto>> GetCouponsForCanteenAsync(int canteenId)
    {
        var coupons = await _couponRepository.GetAllCouponsForCanteenAsync(canteenId);
        return coupons.Select(MapToDto).ToList();
    }

    public async Task<List<PublicCouponDto>> GetActiveCouponsForCustomerAsync(int canteenId)
    {
        var coupons = await _couponRepository.GetActiveCouponsForCanteenAsync(canteenId);
        return coupons.Select(c => new PublicCouponDto
        {
            CouponId = c.CouponId,
            Code = c.Code,
            Title = c.Title,
            Description = c.Description,
            DiscountType = c.DiscountType,
            DiscountValue = c.DiscountValue,
            MinOrderAmount = c.MinOrderAmount,
            MaxDiscountAmount = c.MaxDiscountAmount,
            EndDate = c.EndDate
        }).ToList();
    }

    public async Task<CouponDto> GetCouponByIdAsync(int canteenId, int couponId)
    {
        var coupon = await _couponRepository.GetByIdAsync(couponId);
        if (coupon == null || (coupon.CanteenId.HasValue && coupon.CanteenId.Value != canteenId))
            throw new KeyNotFoundException($"Coupon #{couponId} not found for canteen #{canteenId}.");

        return MapToDto(coupon);
    }

    public async Task<CouponDto> CreateCouponAsync(int canteenId, CreateCouponDto dto)
    {
        var normalizedCode = dto.Code.Trim().ToUpper();

        // Check for duplicate code in same canteen
        var existing = await _couponRepository.GetByCodeAsync(canteenId, normalizedCode);
        if (existing != null && existing.CanteenId == canteenId)
            throw new InvalidOperationException($"A coupon with code '{normalizedCode}' already exists for this canteen.");

        if (dto.EndDate <= dto.StartDate)
            throw new ArgumentException("Expiry date must be after the start date.");

        var coupon = new Coupon
        {
            CanteenId = canteenId,
            Code = normalizedCode,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            DiscountType = dto.DiscountType,
            DiscountValue = dto.DiscountValue,
            MinOrderAmount = dto.MinOrderAmount,
            MaxDiscountAmount = dto.MaxDiscountAmount,
            UsageLimitPerCustomer = dto.UsageLimitPerCustomer,
            TotalUsageLimit = dto.TotalUsageLimit,
            StartDate = dto.StartDate.ToUniversalTime(),
            EndDate = dto.EndDate.ToUniversalTime(),
            IsActive = dto.IsActive,
            TimesUsed = 0,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _couponRepository.AddCouponAsync(coupon);
        return MapToDto(created);
    }

    public async Task<CouponDto> UpdateCouponAsync(int canteenId, int couponId, UpdateCouponDto dto)
    {
        var coupon = await _couponRepository.GetByIdAsync(couponId);
        if (coupon == null || coupon.CanteenId != canteenId)
            throw new KeyNotFoundException($"Coupon #{couponId} not found for canteen #{canteenId}.");

        if (dto.EndDate <= dto.StartDate)
            throw new ArgumentException("Expiry date must be after the start date.");

        coupon.Title = dto.Title.Trim();
        coupon.Description = dto.Description?.Trim();
        coupon.DiscountType = dto.DiscountType;
        coupon.DiscountValue = dto.DiscountValue;
        coupon.MinOrderAmount = dto.MinOrderAmount;
        coupon.MaxDiscountAmount = dto.MaxDiscountAmount;
        coupon.UsageLimitPerCustomer = dto.UsageLimitPerCustomer;
        coupon.TotalUsageLimit = dto.TotalUsageLimit;
        coupon.StartDate = dto.StartDate.ToUniversalTime();
        coupon.EndDate = dto.EndDate.ToUniversalTime();
        coupon.IsActive = dto.IsActive;

        var updated = await _couponRepository.UpdateCouponAsync(coupon);
        return MapToDto(updated);
    }

    public async Task<bool> ToggleCouponStatusAsync(int canteenId, int couponId)
    {
        var coupon = await _couponRepository.GetByIdAsync(couponId);
        if (coupon == null || coupon.CanteenId != canteenId)
            throw new KeyNotFoundException($"Coupon #{couponId} not found for canteen #{canteenId}.");

        coupon.IsActive = !coupon.IsActive;
        await _couponRepository.UpdateCouponAsync(coupon);
        return coupon.IsActive;
    }

    public async Task<bool> DeleteCouponAsync(int canteenId, int couponId)
    {
        var coupon = await _couponRepository.GetByIdAsync(couponId);
        if (coupon == null || coupon.CanteenId != canteenId)
            return false;

        return await _couponRepository.DeleteCouponAsync(couponId);
    }

    public async Task<ValidateCouponResponseDto> ValidateCouponAsync(int canteenId, ValidateCouponRequestDto request)
    {
        var code = (request.Code ?? string.Empty).Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            return new ValidateCouponResponseDto
            {
                IsValid = false,
                Message = "Please enter a valid coupon code."
            };
        }

        var coupon = await _couponRepository.GetByCodeAsync(canteenId, code);
        if (coupon == null)
        {
            return new ValidateCouponResponseDto
            {
                IsValid = false,
                Message = $"Coupon '{code}' is invalid or does not exist."
            };
        }

        // Multi-tenant check
        if (coupon.CanteenId.HasValue && coupon.CanteenId.Value != canteenId)
        {
            return new ValidateCouponResponseDto
            {
                IsValid = false,
                Message = $"Coupon '{code}' is not applicable at this canteen."
            };
        }

        // Active check
        if (!coupon.IsActive)
        {
            return new ValidateCouponResponseDto
            {
                IsValid = false,
                Message = $"Coupon '{code}' is currently inactive."
            };
        }

        // Date validity check
        var now = DateTime.UtcNow;
        if (now < coupon.StartDate)
        {
            return new ValidateCouponResponseDto
            {
                IsValid = false,
                Message = $"Coupon '{code}' offer has not started yet."
            };
        }

        if (now > coupon.EndDate)
        {
            return new ValidateCouponResponseDto
            {
                IsValid = false,
                Message = $"Coupon '{code}' expired on {coupon.EndDate:dd MMM yyyy}."
            };
        }

        // Minimum order amount check
        if (request.FoodSubtotal < coupon.MinOrderAmount)
        {
            var deficit = coupon.MinOrderAmount - request.FoodSubtotal;
            return new ValidateCouponResponseDto
            {
                IsValid = false,
                Message = $"Add ₹{deficit:0.00} more to apply coupon '{code}' (Minimum order: ₹{coupon.MinOrderAmount:0.00}).",
                MinOrderAmount = coupon.MinOrderAmount
            };
        }

        // Total usage limit check
        if (coupon.TotalUsageLimit.HasValue && coupon.TimesUsed >= coupon.TotalUsageLimit.Value)
        {
            return new ValidateCouponResponseDto
            {
                IsValid = false,
                Message = $"Coupon '{code}' redemption limit has been reached."
            };
        }

        // Per-customer usage limit check
        if (!string.IsNullOrWhiteSpace(request.CustomerIdentifier))
        {
            var usageCount = await _couponRepository.GetCustomerRedemptionCountAsync(coupon.CouponId, request.CustomerIdentifier);
            if (usageCount >= coupon.UsageLimitPerCustomer)
            {
                return new ValidateCouponResponseDto
                {
                    IsValid = false,
                    Message = $"You have already used coupon '{code}' the maximum allowed number of times ({coupon.UsageLimitPerCustomer}x)."
                };
            }
        }

        // Valid coupon! Calculate discount
        var discount = CalculateDiscount(coupon, request.FoodSubtotal);
        var netSubtotal = Math.Max(0, request.FoodSubtotal - discount);

        return new ValidateCouponResponseDto
        {
            IsValid = true,
            Message = $"Coupon '{code}' applied! You saved ₹{discount:0.00}.",
            Code = coupon.Code,
            Title = coupon.Title,
            DiscountType = coupon.DiscountType,
            DiscountValue = coupon.DiscountValue,
            DiscountAmount = discount,
            NetFoodSubtotal = netSubtotal,
            MinOrderAmount = coupon.MinOrderAmount,
            MaxDiscountAmount = coupon.MaxDiscountAmount
        };
    }

    public decimal CalculateDiscount(Coupon coupon, decimal foodSubtotal)
    {
        if (foodSubtotal <= 0 || coupon.DiscountValue <= 0)
            return 0.00m;

        decimal discount = 0.00m;

        if (string.Equals(coupon.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase))
        {
            var rawDiscount = foodSubtotal * (coupon.DiscountValue / 100m);

            if (coupon.MaxDiscountAmount.HasValue && coupon.MaxDiscountAmount.Value > 0)
            {
                discount = Math.Min(rawDiscount, coupon.MaxDiscountAmount.Value);
            }
            else
            {
                discount = Math.Min(rawDiscount, foodSubtotal);
            }
        }
        else // "FixedAmount"
        {
            discount = Math.Min(coupon.DiscountValue, foodSubtotal);
        }

        return Math.Round(discount, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<CouponRedemptionReportDto> GetRedemptionReportAsync(int canteenId)
    {
        return await _couponRepository.GetRedemptionReportForCanteenAsync(canteenId);
    }

    private static CouponDto MapToDto(Coupon c)
    {
        return new CouponDto
        {
            CouponId = c.CouponId,
            CanteenId = c.CanteenId,
            Code = c.Code,
            Title = c.Title,
            Description = c.Description,
            DiscountType = c.DiscountType,
            DiscountValue = c.DiscountValue,
            MinOrderAmount = c.MinOrderAmount,
            MaxDiscountAmount = c.MaxDiscountAmount,
            UsageLimitPerCustomer = c.UsageLimitPerCustomer,
            TotalUsageLimit = c.TotalUsageLimit,
            TimesUsed = c.TimesUsed,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt
        };
    }
}

