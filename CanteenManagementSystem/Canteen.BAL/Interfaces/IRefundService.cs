using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IRefundService
{
    /// <summary>
    /// Processes a customer-initiated refund according to the active cancellation policy snapshot.
    /// Deducts platform fee if policy dictates; refunds food subtotal + tax.
    /// </summary>
    Task<RefundDto> ProcessCustomerCancellationRefundAsync(int orderId, string reason);

    /// <summary>
    /// Processes a canteen-initiated cancellation refund (canteen unable to fulfill).
    /// Full 100% refund (food + tax + platform fee).
    /// </summary>
    Task<RefundDto> ProcessCanteenCancellationRefundAsync(int canteenId, int orderId, string reason, int actorOwnerId);

    /// <summary>
    /// Processes a discretionary administrative refund initiated by Super Admin.
    /// </summary>
    Task<RefundDto> ProcessAdminDiscretionaryRefundAsync(int orderId, decimal amount, string reason, int adminOwnerId);

    /// <summary>
    /// Retrieves the refund record for a specific order.
    /// </summary>
    Task<RefundDto?> GetRefundByOrderIdAsync(int orderId);

    /// <summary>
    /// Retrieves all refunds for a specific canteen (Owner scoped).
    /// </summary>
    Task<IEnumerable<RefundDto>> GetRefundsByCanteenAsync(int canteenId);

    /// <summary>
    /// Retrieves all refunds across the platform (Super Admin only).
    /// </summary>
    Task<IEnumerable<RefundDto>> GetAllRefundsAsync();

    /// <summary>
    /// Retrieves aggregate financial metrics for refunds (Total refunded, pending, succeeded, failed).
    /// </summary>
    Task<RefundMetricsSummaryDto> GetRefundMetricsSummaryAsync(int? canteenId = null);

    /// <summary>
    /// Updates a refund status from gateway webhooks (e.g. refund.processed or refund.failed).
    /// </summary>
    Task<bool> UpdateRefundStatusFromGatewayAsync(string gatewayRefundId, string status, string? failureMessage = null);
}

