using System.Threading.Tasks;
using Canteen.Model.DTOs.Reports;

namespace Canteen.DAL.Interfaces;

public interface IReportsRepository
{
    Task<OverviewReportDto> GetOverviewReportAsync(ReportFilterDto filter);
    Task<SalesReportDto> GetSalesReportAsync(ReportFilterDto filter);
    Task<OrderReportDto> GetOrderReportAsync(ReportFilterDto filter);
    Task<ProductReportDto> GetProductReportAsync(ReportFilterDto filter);
    Task<PaymentReportDto> GetPaymentReportAsync(ReportFilterDto filter);
    Task<InventoryReportDto> GetInventoryReportAsync(ReportFilterDto filter);
    Task<CouponReportDto> GetCouponReportAsync(ReportFilterDto filter);
    Task<StaffReportDto> GetStaffReportAsync(ReportFilterDto filter);
    Task<StaffAttendanceReportDto> GetAttendanceReportAsync(ReportFilterDto filter);
    Task<KitchenReportDto> GetKitchenReportAsync(ReportFilterDto filter);
    Task<PeakHourReportDto> GetPeakHourReportAsync(ReportFilterDto filter);
}

