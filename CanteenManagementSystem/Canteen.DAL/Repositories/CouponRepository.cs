using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;

namespace Canteen.DAL.Repositories;

public class CouponRepository : ICouponRepository
{
    private readonly CanteenDbContext _context;

    public CouponRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<Coupon?> GetByIdAsync(int couponId)
    {
        return await _context.Coupons
            .Include(c => c.Canteen)
            .FirstOrDefaultAsync(c => c.CouponId == couponId);
    }

    public async Task<Coupon?> GetByCodeAsync(int? canteenId, string code)
    {
        var normalizedCode = (code ?? string.Empty).Trim().ToUpper();
        if (string.IsNullOrEmpty(normalizedCode))
            return null;

        // If canteenId is provided, first look for a canteen-scoped coupon
        if (canteenId.HasValue && canteenId.Value > 0)
        {
            var canteenCoupon = await _context.Coupons
                .FirstOrDefaultAsync(c => c.CanteenId == canteenId.Value && c.Code.ToUpper() == normalizedCode);

            if (canteenCoupon != null)
                return canteenCoupon;
        }

        // Fall back to platform-wide coupon (CanteenId == null)
        return await _context.Coupons
            .FirstOrDefaultAsync(c => c.CanteenId == null && c.Code.ToUpper() == normalizedCode);
    }

    public async Task<List<Coupon>> GetActiveCouponsForCanteenAsync(int canteenId)
    {
        var now = DateTime.UtcNow;

        return await _context.Coupons
            .Where(c => (c.CanteenId == canteenId || c.CanteenId == null)
                        && c.IsActive
                        && c.StartDate <= now
                        && c.EndDate >= now
                        && (!c.TotalUsageLimit.HasValue || c.TimesUsed < c.TotalUsageLimit.Value))
            .OrderByDescending(c => c.DiscountValue)
            .ToListAsync();
    }

    public async Task<List<Coupon>> GetAllCouponsForCanteenAsync(int canteenId)
    {
        return await _context.Coupons
            .Where(c => c.CanteenId == canteenId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Coupon>> GetPlatformCouponsAsync()
    {
        return await _context.Coupons
            .Where(c => c.CanteenId == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Coupon> AddCouponAsync(Coupon coupon)
    {
        coupon.Code = coupon.Code.Trim().ToUpper();
        coupon.CreatedAt = DateTime.UtcNow;
        _context.Coupons.Add(coupon);
        await _context.SaveChangesAsync();
        return coupon;
    }

    public async Task<Coupon> UpdateCouponAsync(Coupon coupon)
    {
        _context.Coupons.Update(coupon);
        await _context.SaveChangesAsync();
        return coupon;
    }

    public async Task<bool> DeleteCouponAsync(int couponId)
    {
        var coupon = await _context.Coupons.FindAsync(couponId);
        if (coupon == null)
            return false;

        _context.Coupons.Remove(coupon);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<CouponRedemption> AddRedemptionAsync(CouponRedemption redemption)
    {
        redemption.RedeemedAt = DateTime.UtcNow;
        _context.CouponRedemptions.Add(redemption);
        await _context.SaveChangesAsync();
        return redemption;
    }

    public async Task<int> GetCustomerRedemptionCountAsync(int couponId, string customerIdentifier)
    {
        if (string.IsNullOrWhiteSpace(customerIdentifier))
            return 0;

        var normalizedId = customerIdentifier.Trim().ToLower();

        return await _context.CouponRedemptions
            .CountAsync(cr => cr.CouponId == couponId && cr.CustomerIdentifier.ToLower() == normalizedId);
    }

    public async Task<CouponRedemptionReportDto> GetRedemptionReportForCanteenAsync(int canteenId)
    {
        var totalCoupons = await _context.Coupons
            .CountAsync(c => c.CanteenId == canteenId);

        var activeCoupons = await _context.Coupons
            .CountAsync(c => c.CanteenId == canteenId && c.IsActive && c.EndDate >= DateTime.UtcNow);

        var redemptions = await _context.CouponRedemptions
            .Where(cr => cr.CanteenId == canteenId)
            .Select(cr => new { cr.DiscountAmountApplied, cr.OrderId })
            .ToListAsync();

        var totalRedemptions = redemptions.Count;
        var totalDiscountGiven = redemptions.Sum(r => r.DiscountAmountApplied);

        var orderIds = redemptions.Select(r => r.OrderId).Distinct().ToList();
        var totalSales = await _context.Orders
            .Where(o => orderIds.Contains(o.OrderId))
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0.00m;

        return new CouponRedemptionReportDto
        {
            TotalCoupons = totalCoupons,
            ActiveCoupons = activeCoupons,
            TotalRedemptions = totalRedemptions,
            TotalDiscountGiven = totalDiscountGiven,
            TotalSalesWithDiscount = totalSales
        };
    }
}

