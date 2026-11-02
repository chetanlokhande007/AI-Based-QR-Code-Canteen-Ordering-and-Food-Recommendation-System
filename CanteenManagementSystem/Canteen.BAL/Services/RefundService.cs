using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Canteen.BAL.Services;

public class RefundService : IRefundService
{
    private readonly IRefundRepository _refundRepo;
    private readonly IOrderRepository _orderRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IPaymentService _paymentService;
    private readonly IOrderNotificationService? _notificationService;
    private readonly ILogger<RefundService> _logger;

    public RefundService(
        IRefundRepository refundRepo,
        IOrderRepository orderRepo,
        IPaymentRepository paymentRepo,
        IPaymentService paymentService,
        ILogger<RefundService> logger)
        : this(refundRepo, orderRepo, paymentRepo, paymentService, null, logger)
    {
    }

    public RefundService(
        IRefundRepository refundRepo,
        IOrderRepository orderRepo,
        IPaymentRepository paymentRepo,
        IPaymentService paymentService,
        IOrderNotificationService? notificationService,
        ILogger<RefundService> logger)
    {
        _refundRepo = refundRepo;
        _orderRepo = orderRepo;
        _paymentRepo = paymentRepo;
        _paymentService = paymentService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<RefundDto> ProcessCustomerCancellationRefundAsync(int orderId, string reason)
    {
        var order = await _orderRepo.GetByIdAsync(orderId, asNoTracking: false);
        if (order == null)
        {
            throw new KeyNotFoundException($"Order with ID {orderId} was not found.");
        }

        var payment = await _paymentRepo.GetByOrderIdAsync(order.CanteenId, order.OrderId);
        if (payment == null || !string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot refund an order without a confirmed paid transaction.");
        }

        // Prevent duplicate refunds
        var existingRefund = await _refundRepo.GetByOrderIdAsync(order.OrderId);
        if (existingRefund != null && (existingRefund.Status == "Succeeded" || existingRefund.Status == "Processing"))
        {
            _logger.LogWarning("Duplicate refund attempt prevented for Order ID {OrderId}.", orderId);
            return MapToDto(existingRefund);
        }

        // Calculate Customer-initiated refund: Food subtotal + Tax (Platform fee retained)
        decimal nonRefundableFee = string.Equals(order.PlatformFeePolicy, "CustomerPays", StringComparison.OrdinalIgnoreCase)
            ? order.PlatformFeeAmount
            : 0m;
        decimal refundableAmount = Math.Max(0m, order.TotalAmount - nonRefundableFee);

        return await ExecuteRefundAsync(order, payment, refundableAmount, reason, "Customer", null);
    }

    public async Task<RefundDto> ProcessCanteenCancellationRefundAsync(int canteenId, int orderId, string reason, int actorOwnerId)
    {
        var order = await _orderRepo.GetByIdAsync(canteenId, orderId, asNoTracking: false);
        if (order == null)
        {
            throw new KeyNotFoundException($"Order with ID {orderId} was not found for canteen {canteenId}.");
        }

        var payment = await _paymentRepo.GetByOrderIdAsync(canteenId, orderId);
        if (payment == null || !string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot refund an order without a confirmed paid transaction.");
        }

        // Prevent duplicate refunds
        var existingRefund = await _refundRepo.GetByOrderIdAsync(order.OrderId);
        if (existingRefund != null && (existingRefund.Status == "Succeeded" || existingRefund.Status == "Processing"))
        {
            _logger.LogWarning("Duplicate refund attempt prevented for Order ID {OrderId}.", orderId);
            return MapToDto(existingRefund);
        }

        // Canteen-initiated refund: 100% full refund (food + tax + platform fee)
        decimal refundableAmount = order.TotalAmount;

        return await ExecuteRefundAsync(order, payment, refundableAmount, reason, "CanteenOwner", actorOwnerId);
    }

    public async Task<RefundDto> ProcessAdminDiscretionaryRefundAsync(int orderId, decimal amount, string reason, int adminOwnerId)
    {
        var order = await _orderRepo.GetByIdAsync(orderId, asNoTracking: false);
        if (order == null)
        {
            throw new KeyNotFoundException($"Order with ID {orderId} was not found.");
        }

        var payment = await _paymentRepo.GetByOrderIdAsync(order.CanteenId, orderId);
        if (payment == null || !string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot refund an order without a confirmed paid transaction.");
        }

        if (amount <= 0 || amount > order.TotalAmount)
        {
            throw new InvalidOperationException($"Refund amount must be between ₹0.01 and paid total ₹{order.TotalAmount:F2}.");
        }

        return await ExecuteRefundAsync(order, payment, amount, reason, "PlatformAdmin", adminOwnerId);
    }

    private async Task<RefundDto> ExecuteRefundAsync(
        Order order,
        Payment payment,
        decimal amount,
        string reason,
        string initiatedByRole,
        int? actorOwnerId)
    {
        var refund = new Refund
        {
            OrderId = order.OrderId,
            PaymentId = payment.PaymentId,
            CanteenId = order.CanteenId,
            Amount = amount,
            Currency = payment.Currency,
            RefundMethod = payment.PaymentMethod,
            Status = "Processing",
            Reason = string.IsNullOrWhiteSpace(reason) ? "Order cancelled" : reason.Trim(),
            InitiatedByRole = initiatedByRole,
            InitiatedByOwnerId = actorOwnerId,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _refundRepo.AddAsync(refund);

        if (string.Equals(payment.PaymentMethod, "Online", StringComparison.OrdinalIgnoreCase))
        {
            var result = await _paymentService.RefundPaymentAsync(payment, amount, refund.Reason);
            if (result.Success)
            {
                created.Status = result.Status;
                created.GatewayRefundId = result.GatewayRefundId;
                created.ProcessedAt = DateTime.UtcNow;
            }
            else
            {
                created.Status = "Failed";
                created.FailureMessage = result.FailureReason;
                _logger.LogError("Gateway refund failed for Order {OrderId}: {FailureReason}", order.OrderId, result.FailureReason);
            }
        }
        else
        {
            // Counter Cash Refund
            created.Status = "Succeeded";
            created.GatewayRefundId = $"cash_rfnd_{order.OrderId}_{Guid.NewGuid():N}[..16]";
            created.ProcessedAt = DateTime.UtcNow;
        }

        await _refundRepo.UpdateAsync(created);

        // Update payment status
        if (created.Status == "Succeeded")
        {
            payment.Status = "Refunded";
            payment.FailureReason = $"Refunded ₹{amount:F2} ({created.Reason})";
            await _paymentRepo.UpdateAsync(payment);
        }

        var dto = MapToDto(created);
        dto.OrderNumber = order.OrderNumber;
        dto.CanteenName = order.Canteen?.Name ?? string.Empty;

        // Broadcast real-time refund status event
        if (_notificationService != null)
        {
            await _notificationService.NotifyOrderStatusUpdatedAsync(
                order.OrderId,
                order.OrderNumber,
                order.Status,
                DateTime.UtcNow);
        }

        return dto;
    }

    public async Task<RefundDto?> GetRefundByOrderIdAsync(int orderId)
    {
        var entity = await _refundRepo.GetByOrderIdAsync(orderId);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<IEnumerable<RefundDto>> GetRefundsByCanteenAsync(int canteenId)
    {
        var list = await _refundRepo.GetAllByCanteenAsync(canteenId);
        return list.Select(MapToDto);
    }

    public async Task<IEnumerable<RefundDto>> GetAllRefundsAsync()
    {
        var list = await _refundRepo.GetAllAsync();
        return list.Select(MapToDto);
    }

    public async Task<RefundMetricsSummaryDto> GetRefundMetricsSummaryAsync(int? canteenId = null)
    {
        var list = canteenId.HasValue
            ? await _refundRepo.GetAllByCanteenAsync(canteenId.Value)
            : await _refundRepo.GetAllAsync();

        var refundsList = list.ToList();
        return new RefundMetricsSummaryDto
        {
            TotalRefundAmount = refundsList.Where(r => r.Status == "Succeeded").Sum(r => r.Amount),
            TotalRefundsCount = refundsList.Count,
            PendingCount = refundsList.Count(r => r.Status == "Pending" || r.Status == "Processing"),
            SucceededCount = refundsList.Count(r => r.Status == "Succeeded"),
            FailedCount = refundsList.Count(r => r.Status == "Failed")
        };
    }

    public async Task<bool> UpdateRefundStatusFromGatewayAsync(string gatewayRefundId, string status, string? failureMessage = null)
    {
        var refund = await _refundRepo.GetByGatewayRefundIdAsync(gatewayRefundId);
        if (refund == null)
        {
            return false;
        }

        refund.Status = status;
        if (status == "Succeeded")
        {
            refund.ProcessedAt = DateTime.UtcNow;
            refund.FailureMessage = null;
        }
        else if (status == "Failed")
        {
            refund.FailureMessage = failureMessage;
        }

        await _refundRepo.UpdateAsync(refund);
        return true;
    }

    private static RefundDto MapToDto(Refund r) => new()
    {
        RefundId = r.RefundId,
        OrderId = r.OrderId,
        OrderNumber = r.Order?.OrderNumber ?? string.Empty,
        PaymentId = r.PaymentId,
        CanteenId = r.CanteenId,
        CanteenName = r.Canteen?.Name ?? string.Empty,
        Amount = r.Amount,
        Currency = r.Currency,
        RefundMethod = r.RefundMethod,
        Status = r.Status,
        GatewayRefundId = r.GatewayRefundId,
        Reason = r.Reason,
        InitiatedByRole = r.InitiatedByRole,
        FailureMessage = r.FailureMessage,
        CreatedAt = r.CreatedAt,
        ProcessedAt = r.ProcessedAt
    };
}

