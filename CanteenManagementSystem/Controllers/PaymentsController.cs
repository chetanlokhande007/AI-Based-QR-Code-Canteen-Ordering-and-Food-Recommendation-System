using System;
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

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
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
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayment(int canteenId, int orderId)
    {
        var ownerCanteenId = User.GetCanteenId();
        if (ownerCanteenId == null || ownerCanteenId.Value != canteenId)
        {
            return Forbid();
        }

        var payment = await _paymentService.GetPaymentByOrderIdAsync(canteenId, orderId);
        return payment is null ? NotFound(new { message = "No payment record found for this order." }) : Ok(payment);
    }
}
