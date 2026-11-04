using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs.Reports;

namespace Canteen.BAL.Services;

public class ReportsService : IReportsService
{
    private readonly IReportsRepository _reportsRepository;

    public ReportsService(IReportsRepository reportsRepository)
    {
        _reportsRepository = reportsRepository;
    }

    public Task<OverviewReportDto> GetOverviewReportAsync(ReportFilterDto filter)
    {
        return _reportsRepository.GetOverviewReportAsync(filter);
    }

    public Task<SalesReportDto> GetSalesReportAsync(ReportFilterDto filter)
    {
        return _reportsRepository.GetSalesReportAsync(filter);
    }

    public Task<OrderReportDto> GetOrderReportAsync(ReportFilterDto filter)
    {
        return _reportsRepository.GetOrderReportAsync(filter);
    }

    public Task<ProductReportDto> GetProductReportAsync(ReportFilterDto filter)
    {
        return _reportsRepository.GetProductReportAsync(filter);
    }

    public Task<PaymentReportDto> GetPaymentReportAsync(ReportFilterDto filter)
    {
        return _reportsRepository.GetPaymentReportAsync(filter);
    }

    public Task<InventoryReportDto> GetInventoryReportAsync(ReportFilterDto filter)
    {
        return _reportsRepository.GetInventoryReportAsync(filter);
    }

    public Task<CouponReportDto> GetCouponReportAsync(ReportFilterDto filter)
    {
        return _reportsRepository.GetCouponReportAsync(filter);
    }

    public Task<StaffReportDto> GetStaffReportAsync(ReportFilterDto filter)
    {
        return _reportsRepository.GetStaffReportAsync(filter);
    }

    public Task<StaffAttendanceReportDto> GetAttendanceReportAsync(ReportFilterDto filter)
    {
        return _reportsRepository.GetAttendanceReportAsync(filter);
    }

    public Task<KitchenReportDto> GetKitchenReportAsync(ReportFilterDto filter)
    {
        return _reportsRepository.GetKitchenReportAsync(filter);
    }

    public Task<PeakHourReportDto> GetPeakHourReportAsync(ReportFilterDto filter)
    {
        return _reportsRepository.GetPeakHourReportAsync(filter);
    }

    public async Task<ReportExportResultDto> ExportReportAsync(ExportReportRequestDto request)
    {
        // For exports, request a large page size to include all records
        var exportFilter = new ReportFilterDto
        {
            CanteenId = request.CanteenId,
            Period = request.Period,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Page = 1,
            PageSize = 5000,
            Search = request.Search
        };

        var headers = new List<string>();
        var rows = new List<List<string>>();
        var title = $"{request.ReportType.ToUpperInvariant()} REPORT";
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var reportName = $"{request.ReportType.ToLowerInvariant()}_report_{timestamp}";

        switch (request.ReportType.ToLowerInvariant())
        {
            case "overview":
                var overview = await _reportsRepository.GetOverviewReportAsync(exportFilter);
                title = $"Executive Overview Report - {overview.CanteenName}";
                headers = new List<string> { "Metric", "Value", "Baseline Growth (%)" };
                rows.Add(new List<string> { "Gross Revenue", overview.GrossRevenue.ToString("F2"), $"{overview.GrossRevenueGrowthPercent}%" });
                rows.Add(new List<string> { "Net Revenue", overview.NetRevenue.ToString("F2"), $"{overview.NetRevenueGrowthPercent}%" });
                rows.Add(new List<string> { "Total Refunds", overview.TotalRefunds.ToString("F2"), "-" });
                rows.Add(new List<string> { "Total Orders", overview.TotalOrders.ToString(), $"{overview.OrdersGrowthPercent}%" });
                rows.Add(new List<string> { "Completed Orders", overview.CompletedOrders.ToString(), "-" });
                rows.Add(new List<string> { "Cancelled Orders", overview.CancelledOrders.ToString(), "-" });
                rows.Add(new List<string> { "Average Order Value", overview.AverageOrderValue.ToString("F2"), $"{overview.AverageOrderValueGrowthPercent}%" });
                rows.Add(new List<string> { "Active Products Count", overview.ActiveProductsCount.ToString(), "-" });
                break;

            case "sales":
                var sales = await _reportsRepository.GetSalesReportAsync(exportFilter);
                title = $"Sales & Revenue Report - {sales.CanteenName}";
                headers = new List<string> { "Order #", "Table", "Date (UTC)", "Subtotal", "Discount", "Tax", "Total Amount", "Refunded", "Net Amount", "Status", "Payment Status" };
                foreach (var item in sales.Transactions.Items)
                {
                    rows.Add(new List<string>
                    {
                        item.OrderNumber,
                        item.TableNumber,
                        item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        item.Subtotal.ToString("F2"),
                        item.Discount.ToString("F2"),
                        item.Tax.ToString("F2"),
                        item.TotalAmount.ToString("F2"),
                        item.RefundedAmount.ToString("F2"),
                        item.NetAmount.ToString("F2"),
                        item.OrderStatus,
                        item.PaymentStatus
                    });
                }
                break;

            case "orders":
                var orders = await _reportsRepository.GetOrderReportAsync(exportFilter);
                title = $"Orders Report - {orders.CanteenName}";
                headers = new List<string> { "Order #", "Table", "Items Count", "Food Subtotal", "Discount", "Tax", "Total Amount", "Status", "Payment Status", "Cancellation Reason", "Created At (UTC)" };
                foreach (var item in orders.Orders.Items)
                {
                    rows.Add(new List<string>
                    {
                        item.OrderNumber,
                        item.TableNumber,
                        item.TotalItems.ToString(),
                        item.SubtotalAmount.ToString("F2"),
                        item.DiscountAmount.ToString("F2"),
                        item.TaxAmount.ToString("F2"),
                        item.TotalAmount.ToString("F2"),
                        item.Status,
                        item.PaymentStatus,
                        item.CancellationReason ?? "-",
                        item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
                break;

            case "products":
                var products = await _reportsRepository.GetProductReportAsync(exportFilter);
                title = $"Product Performance Report - {products.CanteenName}";
                headers = new List<string> { "Product ID", "Name", "Category", "Food Type", "Unit Price", "Units Sold", "Total Revenue", "Current Stock", "Status", "Revenue Share (%)" };
                foreach (var item in products.Products.Items)
                {
                    rows.Add(new List<string>
                    {
                        item.ProductId.ToString(),
                        item.Name,
                        item.CategoryName,
                        item.FoodType,
                        item.UnitPrice.ToString("F2"),
                        item.QuantitySold.ToString(),
                        item.TotalRevenue.ToString("F2"),
                        item.CurrentStock.ToString(),
                        item.IsAvailable ? "Available" : "Unavailable",
                        $"{item.RevenueSharePercentage}%"
                    });
                }
                break;

            case "payments":
                var payments = await _reportsRepository.GetPaymentReportAsync(exportFilter);
                title = $"Payments & Settlements Ledger - {payments.CanteenName}";
                headers = new List<string> { "Payment ID", "Order #", "Amount", "Currency", "Method", "Status", "Razorpay Payment ID", "Created At (UTC)", "Paid At (UTC)", "Refunded Amount", "Refund Status" };
                foreach (var item in payments.Payments.Items)
                {
                    rows.Add(new List<string>
                    {
                        item.PaymentId.ToString(),
                        item.OrderNumber,
                        item.Amount.ToString("F2"),
                        item.Currency,
                        item.PaymentMethod,
                        item.Status,
                        item.RazorpayPaymentId ?? "-",
                        item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        item.PaidAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-",
                        item.RefundedAmount.ToString("F2"),
                        item.RefundStatus
                    });
                }
                break;

            case "inventory":
                var inventory = await _reportsRepository.GetInventoryReportAsync(exportFilter);
                title = $"Inventory Valuation & Stock Report - {inventory.CanteenName}";
                headers = new List<string> { "Ingredient ID", "Name", "Category", "Unit", "Current Stock", "Reserved", "Available", "Min Level", "Cost/Unit", "Valuation", "Stock Status" };
                foreach (var item in inventory.Ingredients.Items)
                {
                    rows.Add(new List<string>
                    {
                        item.IngredientId.ToString(),
                        item.Name,
                        item.Category,
                        item.BaseUnit,
                        item.CurrentStock.ToString("F2"),
                        item.ReservedStock.ToString("F2"),
                        item.AvailableStock.ToString("F2"),
                        item.MinimumStockLevel.ToString("F2"),
                        item.CostPerBaseUnit.ToString("F2"),
                        item.Valuation.ToString("F2"),
                        item.IsLowStock ? "LOW STOCK" : "Optimal"
                    });
                }
                break;

            case "coupons":
                var coupons = await _reportsRepository.GetCouponReportAsync(exportFilter);
                title = $"Coupons & Offers Report - {coupons.CanteenName}";
                headers = new List<string> { "Redemption ID", "Coupon Code", "Order #", "Customer Identifier", "Discount Applied", "Order Total", "Redeemed At (UTC)" };
                foreach (var item in coupons.Redemptions.Items)
                {
                    rows.Add(new List<string>
                    {
                        item.RedemptionId.ToString(),
                        item.CouponCode,
                        item.OrderNumber,
                        item.CustomerIdentifier,
                        item.DiscountAmountApplied.ToString("F2"),
                        item.OrderTotal.ToString("F2"),
                        item.RedeemedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
                break;

            case "staff":
                var staff = await _reportsRepository.GetStaffReportAsync(exportFilter);
                title = $"Staff Performance & Attendance Report - {staff.CanteenName}";
                headers = new List<string> { "Staff ID", "Employee ID", "Full Name", "Role", "Shift", "Status", "Joined On", "Salary", "Present Days", "Absent Days", "Attendance Rate (%)" };
                foreach (var item in staff.StaffMembers.Items)
                {
                    rows.Add(new List<string>
                    {
                        item.StaffId.ToString(),
                        item.EmployeeId,
                        item.FullName,
                        item.Role,
                        item.Shift,
                        item.Status,
                        item.JoinedOn.ToString("yyyy-MM-dd"),
                        item.Salary?.ToString("F2") ?? "-",
                        item.PresentDays.ToString(),
                        item.AbsentDays.ToString(),
                        $"{item.AttendanceRate}%"
                    });
                }
                break;

            case "attendance":
                var attendance = await _reportsRepository.GetAttendanceReportAsync(exportFilter);
                title = $"Staff Attendance Log - {attendance.CanteenName}";
                headers = new List<string> { "Log ID", "Employee ID", "Staff Name", "Role", "Date", "Check-In", "Check-Out", "Status", "Notes" };
                foreach (var item in attendance.LogEntries.Items)
                {
                    rows.Add(new List<string>
                    {
                        item.AttendanceId.ToString(),
                        item.EmployeeId,
                        item.FullName,
                        item.Role,
                        item.Date.ToString("yyyy-MM-dd"),
                        item.CheckInTime?.ToString("HH:mm:ss") ?? "-",
                        item.CheckOutTime?.ToString("HH:mm:ss") ?? "-",
                        item.Status,
                        item.Notes ?? "-"
                    });
                }
                break;

            case "kitchen":
                var kitchen = await _reportsRepository.GetKitchenReportAsync(exportFilter);
                title = $"Kitchen Efficiency & Order Queue Report - {kitchen.CanteenName}";
                headers = new List<string> { "Order ID", "Order #", "Table", "Items Count", "Status", "Duration (Mins)", "Is Delayed", "Created At (UTC)" };
                foreach (var item in kitchen.OrderQueue.Items)
                {
                    rows.Add(new List<string>
                    {
                        item.OrderId.ToString(),
                        item.OrderNumber,
                        item.TableNumber,
                        item.TotalItemsCount.ToString(),
                        item.Status,
                        item.DurationMinutes.ToString(),
                        item.IsDelayed ? "YES" : "NO",
                        item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
                break;

            case "peak-hours":
                var peak = await _reportsRepository.GetPeakHourReportAsync(exportFilter);
                title = $"Peak Hours & Traffic Heatmap - {peak.CanteenName}";
                headers = new List<string> { "Hour", "Hour Label", "Orders Count", "Gross Sales", "Average Order Value", "Traffic Share (%)", "Is Peak" };
                foreach (var item in peak.HourlyTraffic)
                {
                    rows.Add(new List<string>
                    {
                        item.Hour.ToString(),
                        item.HourLabel,
                        item.OrdersCount.ToString(),
                        item.GrossSales.ToString("F2"),
                        item.AverageOrderValue.ToString("F2"),
                        $"{item.TrafficPercentage}%",
                        item.IsPeakHour ? "PEAK" : "-"
                    });
                }
                break;

            default:
                var defSales = await _reportsRepository.GetSalesReportAsync(exportFilter);
                title = $"Sales Report - {defSales.CanteenName}";
                headers = new List<string> { "Order #", "Table", "Date", "Total Amount", "Status" };
                foreach (var item in defSales.Transactions.Items)
                {
                    rows.Add(new List<string> { item.OrderNumber, item.TableNumber, item.CreatedAt.ToString("yyyy-MM-dd"), item.TotalAmount.ToString("F2"), item.OrderStatus });
                }
                break;
        }

        var isExcel = string.Equals(request.Format, "excel", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(request.Format, "xlsx", StringComparison.OrdinalIgnoreCase);

        if (isExcel)
        {
            var excelBytes = GenerateXmlSpreadsheet(title, headers, rows);
            return new ReportExportResultDto
            {
                FileBytes = excelBytes,
                ContentType = "application/vnd.ms-excel",
                FileName = $"{reportName}.xls"
            };
        }
        else
        {
            var csvBytes = GenerateCsv(headers, rows);
            return new ReportExportResultDto
            {
                FileBytes = csvBytes,
                ContentType = "text/csv",
                FileName = $"{reportName}.csv"
            };
        }
    }

    private static byte[] GenerateCsv(List<string> headers, List<List<string>> rows)
    {
        var sb = new StringBuilder();

        // Write header row
        sb.AppendLine(string.Join(",", headers.ConvertAll(EscapeCsvValue)));

        // Write data rows
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.ConvertAll(EscapeCsvValue)));
        }

        // Return with UTF-8 BOM so Microsoft Excel renders accented and special characters cleanly
        var preamble = Encoding.UTF8.GetPreamble();
        var data = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + data.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(data, 0, result, preamble.Length, data.Length);
        return result;
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static byte[] GenerateXmlSpreadsheet(string sheetTitle, List<string> headers, List<List<string>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");

        sb.AppendLine(" <Styles>");
        sb.AppendLine("  <Style ss:ID=\"Default\" ss:Name=\"Normal\">");
        sb.AppendLine("   <Alignment ss:Vertical=\"Bottom\"/>");
        sb.AppendLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"10\" ss:Color=\"#1e293b\"/>");
        sb.AppendLine("  </Style>");
        sb.AppendLine("  <Style ss:ID=\"HeaderStyle\">");
        sb.AppendLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"10\" ss:Bold=\"1\" ss:Color=\"#ffffff\"/>");
        sb.AppendLine("   <Interior ss:Color=\"#0f172a\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("  </Style>");
        sb.AppendLine("  <Style ss:ID=\"TitleStyle\">");
        sb.AppendLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"13\" ss:Bold=\"1\" ss:Color=\"#0f172a\"/>");
        sb.AppendLine("  </Style>");
        sb.AppendLine(" </Styles>");

        sb.AppendLine($" <Worksheet ss:Name=\"Report\">");
        sb.AppendLine("  <Table>");

        // Title row
        sb.AppendLine("   <Row>");
        sb.AppendLine($"    <Cell ss:StyleID=\"TitleStyle\"><Data ss:Type=\"String\">{EscapeXml(sheetTitle)}</Data></Cell>");
        sb.AppendLine("   </Row>");
        sb.AppendLine("   <Row></Row>"); // Blank spacer

        // Header row
        sb.AppendLine("   <Row>");
        foreach (var header in headers)
        {
            sb.AppendLine($"    <Cell ss:StyleID=\"HeaderStyle\"><Data ss:Type=\"String\">{EscapeXml(header)}</Data></Cell>");
        }
        sb.AppendLine("   </Row>");

        // Data rows
        foreach (var row in rows)
        {
            sb.AppendLine("   <Row>");
            foreach (var cell in row)
            {
                var isNumeric = decimal.TryParse(cell, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
                var type = isNumeric ? "Number" : "String";
                sb.AppendLine($"    <Cell><Data ss:Type=\"{type}\">{EscapeXml(cell)}</Data></Cell>");
            }
            sb.AppendLine("   </Row>");
        }

        sb.AppendLine("  </Table>");
        sb.AppendLine(" </Worksheet>");
        sb.AppendLine("</Workbook>");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}

