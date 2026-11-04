using System;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs.Reports;
using CanteenManagementSystem.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "CanteenOwner,PlatformAdmin")]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly IReportsService _reportsService;
    private readonly ICanteenService _canteenService;

    public ReportsController(IReportsService reportsService, ICanteenService canteenService)
    {
        _reportsService = reportsService;
        _canteenService = canteenService;
    }

    private async Task<(bool authorized, int canteenId, IActionResult? failureResult)> ResolveAndAuthorizeCanteenAsync(int? queryCanteenId)
    {
        int resolvedId = queryCanteenId ?? User.GetCanteenId() ?? 0;
        if (resolvedId <= 0)
        {
            return (false, 0, BadRequest(new { message = "A valid canteenId must be provided in the query string or authenticated token." }));
        }

        if (User.IsInRole("PlatformAdmin"))
        {
            return (true, resolvedId, null);
        }

        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue)
        {
            return (false, 0, Forbid());
        }

        var tokenCanteenId = User.GetCanteenId();
        if (!tokenCanteenId.HasValue || tokenCanteenId.Value != resolvedId)
        {
            return (false, 0, Forbid());
        }

        var isAuthorized = await _canteenService.IsOwnerAuthorizedAndActiveAsync(ownerId.Value, resolvedId);
        if (!isAuthorized)
        {
            return (false, 0, Forbid());
        }

        return (true, resolvedId, null);
    }

    /// <summary>
    /// Executive summary report with KPIs, comparative growth, and top-level distributions.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(OverviewReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOverviewReport([FromQuery] ReportFilterDto filter)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(filter.CanteenId > 0 ? filter.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        filter.CanteenId = auth.canteenId;
        var report = await _reportsService.GetOverviewReportAsync(filter);
        return Ok(report);
    }

    /// <summary>
    /// Financial and sales report with gross/net sales, taxes, platform fees, canteen earnings, and daily trend.
    /// </summary>
    [HttpGet("sales")]
    [ProducesResponseType(typeof(SalesReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSalesReport([FromQuery] ReportFilterDto filter)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(filter.CanteenId > 0 ? filter.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        filter.CanteenId = auth.canteenId;
        var report = await _reportsService.GetSalesReportAsync(filter);
        return Ok(report);
    }

    /// <summary>
    /// Orders report with fulfillment rate, cancellation breakdown, and detailed order records.
    /// </summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(OrderReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrderReport([FromQuery] ReportFilterDto filter)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(filter.CanteenId > 0 ? filter.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        filter.CanteenId = auth.canteenId;
        var report = await _reportsService.GetOrderReportAsync(filter);
        return Ok(report);
    }

    /// <summary>
    /// Food product catalog performance, top sellers, slow moving items, and category revenue share.
    /// </summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(ProductReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProductReport([FromQuery] ReportFilterDto filter)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(filter.CanteenId > 0 ? filter.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        filter.CanteenId = auth.canteenId;
        var report = await _reportsService.GetProductReportAsync(filter);
        return Ok(report);
    }

    /// <summary>
    /// Payment collections, Razorpay vs cash counter settlement, and refund ledger.
    /// </summary>
    [HttpGet("payments")]
    [ProducesResponseType(typeof(PaymentReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaymentReport([FromQuery] ReportFilterDto filter)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(filter.CanteenId > 0 ? filter.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        filter.CanteenId = auth.canteenId;
        var report = await _reportsService.GetPaymentReportAsync(filter);
        return Ok(report);
    }

    /// <summary>
    /// Inventory stock valuation, low stock warning alerts, and kitchen wastage analysis.
    /// </summary>
    [HttpGet("inventory")]
    [ProducesResponseType(typeof(InventoryReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInventoryReport([FromQuery] ReportFilterDto filter)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(filter.CanteenId > 0 ? filter.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        filter.CanteenId = auth.canteenId;
        var report = await _reportsService.GetInventoryReportAsync(filter);
        return Ok(report);
    }

    /// <summary>
    /// Promotional coupons performance, redemption counts, discount costs, and ROI.
    /// </summary>
    [HttpGet("coupons")]
    [ProducesResponseType(typeof(CouponReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCouponReport([FromQuery] ReportFilterDto filter)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(filter.CanteenId > 0 ? filter.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        filter.CanteenId = auth.canteenId;
        var report = await _reportsService.GetCouponReportAsync(filter);
        return Ok(report);
    }

    /// <summary>
    /// Staff directory, shift distribution, attendance rate, and payroll summary.
    /// </summary>
    [HttpGet("staff")]
    [ProducesResponseType(typeof(StaffReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStaffReport([FromQuery] ReportFilterDto filter)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(filter.CanteenId > 0 ? filter.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        filter.CanteenId = auth.canteenId;
        var report = await _reportsService.GetStaffReportAsync(filter);
        return Ok(report);
    }

    /// <summary>
    /// Staff attendance records, daily presence trends, and check-in/out logs.
    /// </summary>
    [HttpGet("attendance")]
    [ProducesResponseType(typeof(StaffAttendanceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAttendanceReport([FromQuery] ReportFilterDto filter)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(filter.CanteenId > 0 ? filter.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        filter.CanteenId = auth.canteenId;
        var report = await _reportsService.GetAttendanceReportAsync(filter);
        return Ok(report);
    }

    /// <summary>
    /// Kitchen preparation velocity, average turnaround times, delayed orders, and top dishes cooked.
    /// </summary>
    [HttpGet("kitchen")]
    [ProducesResponseType(typeof(KitchenReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetKitchenReport([FromQuery] ReportFilterDto filter)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(filter.CanteenId > 0 ? filter.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        filter.CanteenId = auth.canteenId;
        var report = await _reportsService.GetKitchenReportAsync(filter);
        return Ok(report);
    }

    /// <summary>
    /// 24-hour customer traffic heatmap, peak rush hours, and dining table utilization.
    /// </summary>
    [HttpGet("peak-hours")]
    [ProducesResponseType(typeof(PeakHourReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPeakHourReport([FromQuery] ReportFilterDto filter)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(filter.CanteenId > 0 ? filter.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        filter.CanteenId = auth.canteenId;
        var report = await _reportsService.GetPeakHourReportAsync(filter);
        return Ok(report);
    }

    /// <summary>
    /// Exports any report as a CSV or Excel spreadsheet file.
    /// </summary>
    [HttpGet("export")]
    [Produces("text/csv", "application/vnd.ms-excel")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportReport([FromQuery] ExportReportRequestDto request)
    {
        var auth = await ResolveAndAuthorizeCanteenAsync(request.CanteenId > 0 ? request.CanteenId : null);
        if (!auth.authorized) return auth.failureResult!;

        request.CanteenId = auth.canteenId;
        var exportResult = await _reportsService.ExportReportAsync(request);
        return File(exportResult.FileBytes, exportResult.ContentType, exportResult.FileName);
    }
}

