using System.Collections.Generic;
using System.Threading.Tasks;
using Canteen.Model.DTOs;

namespace Canteen.BAL.Interfaces;

public interface IPaymentDashboardService
{
    Task<PaymentDashboardSummaryDto> GetDashboardSummaryAsync(int canteenId, string? dateRange);

    Task<PaymentPagedResult<PaymentTransactionDto>> GetTransactionsAsync(
        int canteenId,
        int page,
        int pageSize,
        string? search,
        string? status,
        string? paymentMethod,
        string? dateRange);

    Task<CanteenSettlementSummaryDto> GetSettlementSummaryAsync(int canteenId);

    Task<List<PaymentMethodConfigDto>> GetPaymentMethodsAsync(int canteenId);

    Task<CanteenPaymentSettingsDto?> GetPaymentSettingsAsync(int canteenId);

    Task<CanteenPaymentSettingsDto> UpdatePaymentSettingsAsync(int canteenId, UpdatePaymentSettingsDto dto);

    Task<byte[]> ExportStatementCsvAsync(int canteenId, string? dateRange);

    Task<PaymentReconciliationResultDto> ReconcilePaymentsAsync(int canteenId);
}

