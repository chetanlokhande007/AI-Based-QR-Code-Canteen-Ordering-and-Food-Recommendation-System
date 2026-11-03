using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Canteen.BAL.Exceptions;
using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using CanteenManagementSystem.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ICanteenService _canteenService;
    private readonly IPaymentDashboardService _paymentDashboardService;

    public PaymentsController(
        IPaymentService paymentService,
        ICanteenService canteenService,
        IPaymentDashboardService paymentDashboardService)
    {
        _paymentService = paymentService;
        _canteenService = canteenService;
        _paymentDashboardService = paymentDashboardService;
    }

    private async Task<bool> IsAuthorizedAsync(int canteenId)
    {
        if (User.IsInRole("PlatformAdmin")) return true;

        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue) return false;

        var activeCanteenId = User.GetCanteenId();
        if (!activeCanteenId.HasValue || activeCanteenId.Value != canteenId)
        {
            return false;
        }

        return await _canteenService.IsOwnerAuthorizedAndActiveAsync(ownerId.Value, canteenId);
    }

    /// <summary>
    /// Creates a Razorpay payment order for an existing customer order using the server-calculated amount.
    /// </summary>
    [HttpPost("api/payments/create-order")]
    [ProducesResponseType(typeof(PaymentOrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePaymentOrder([FromBody] CreatePaymentOrderDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _paymentService.CreatePaymentOrderAsync(dto.OrderId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Creates or reuses a pending Pay at Counter payment for an existing customer order.
    /// </summary>
    [HttpPost("api/payments/pay-at-counter")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PayAtCounter([FromBody] CreatePaymentOrderDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _paymentService.CreateCounterPaymentAsync(dto.OrderId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Verifies the Razorpay payment signature after successful checkout.
    /// </summary>
    [HttpPost("api/payments/verify")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _paymentService.VerifyPaymentAsync(dto);
            return Ok(result);
        }
        catch (PaymentVerificationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Receives and processes Razorpay webhook events with signature verification and idempotency.
    /// </summary>
    [HttpPost("api/payments/webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Webhook([FromHeader(Name = "X-Razorpay-Signature")] string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest(new { message = "Missing 'X-Razorpay-Signature' header." });
        }

        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return BadRequest(new { message = "Empty webhook payload." });
        }

        try
        {
            await _paymentService.ProcessWebhookAsync(rawBody, signature);
            return Ok(new { status = "ok" });
        }
        catch (PaymentVerificationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves payment status for an order (Canteen Owner only).
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/orders/{orderId:int}/payment")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayment(int canteenId, int orderId)
    {
        if (!await IsAuthorizedAsync(canteenId))
        {
            return Forbid();
        }

        var payment = await _paymentService.GetPaymentByOrderIdAsync(canteenId, orderId);
        return payment is null ? NotFound(new { message = "No payment record found for this order." }) : Ok(payment);
    }

    /// <summary>
    /// Confirms cash collection for a pending counter payment.
    /// Canteen Owner only.
    /// </summary>
    [HttpPost("api/canteens/{canteenId:int}/orders/{orderId:int}/confirm-counter-payment")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmCounterPayment(
        int canteenId,
        int orderId)
    {
        if (!await IsAuthorizedAsync(canteenId))
            return Forbid();

        try
        {
            var result = await _paymentService.ConfirmCounterPaymentAsync(
                canteenId,
                orderId);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PAYMENTS DASHBOARD API ENDPOINTS (API-DRIVEN, ZERO HARDCODED VALUES)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves authoritative payment KPI summary metrics for the canteen dashboard.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/payments/dashboard-summary")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(PaymentDashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboardSummary(int canteenId, [FromQuery] string? dateRange = "Last 7 Days")
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var summary = await _paymentDashboardService.GetDashboardSummaryAsync(canteenId, dateRange);
        return Ok(summary);
    }

    /// <summary>
    /// Retrieves server-paginated, filtered payment transactions.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/payments/transactions")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(PaymentPagedResult<PaymentTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTransactions(
        int canteenId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? paymentMethod = null,
        [FromQuery] string? dateRange = "Last 7 Days")
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var result = await _paymentDashboardService.GetTransactionsAsync(
            canteenId, page, pageSize, search, status, paymentMethod, dateRange);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves settlement balance and payout details for the canteen.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/payments/settlements")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(CanteenSettlementSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSettlementSummary(int canteenId)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var result = await _paymentDashboardService.GetSettlementSummaryAsync(canteenId);
        return Ok(result);
    }

    /// <summary>
    /// Lists active payment methods configured for this canteen.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/payments/methods")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(List<PaymentMethodConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaymentMethods(int canteenId)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var result = await _paymentDashboardService.GetPaymentMethodsAsync(canteenId);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves bank and fee settlement settings for the canteen.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/payments/settings")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(CanteenPaymentSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentSettings(int canteenId)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var result = await _paymentDashboardService.GetPaymentSettingsAsync(canteenId);
        return result is null ? NotFound(new { message = "Canteen not found." }) : Ok(result);
    }

    /// <summary>
    /// Updates bank and fee settlement settings for the canteen.
    /// </summary>
    [HttpPut("api/canteens/{canteenId:int}/payments/settings")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(CanteenPaymentSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePaymentSettings(int canteenId, [FromBody] UpdatePaymentSettingsDto dto)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        try
        {
            var result = await _paymentDashboardService.UpdatePaymentSettingsAsync(canteenId, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Exports payment transactions as a CSV statement.
    /// </summary>
    [HttpGet("api/canteens/{canteenId:int}/payments/export")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportStatement(int canteenId, [FromQuery] string? dateRange = "Last 7 Days")
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var bytes = await _paymentDashboardService.ExportStatementCsvAsync(canteenId, dateRange);
        var safeRange = dateRange?.Replace(" ", "_") ?? "all";
        var filename = $"payments_statement_canteen_{canteenId}_{safeRange}.csv";
        return File(bytes, "text/csv", filename);
    }

    /// <summary>
    /// Reconciles pending transactions against gateway status.
    /// </summary>
    [HttpPost("api/canteens/{canteenId:int}/payments/reconcile")]
    [Authorize(Roles = "CanteenOwner,PlatformAdmin")]
    [ProducesResponseType(typeof(PaymentReconciliationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReconcilePayments(int canteenId)
    {
        if (!await IsAuthorizedAsync(canteenId)) return Forbid();
        var result = await _paymentDashboardService.ReconcilePaymentsAsync(canteenId);
        return Ok(result);
    }
}
