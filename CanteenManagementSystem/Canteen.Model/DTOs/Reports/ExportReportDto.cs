namespace Canteen.Model.DTOs.Reports;

public class ExportReportRequestDto : ReportFilterDto
{
    /// <summary>
    /// Type of report to export: "overview", "sales", "orders", "products", "payments", "inventory", "coupons", "staff", "attendance", "kitchen", "peak-hours".
    /// </summary>
    public string ReportType { get; set; } = "sales";

    /// <summary>
    /// Format: "csv" or "excel". Defaults to "csv".
    /// </summary>
    public string Format { get; set; } = "csv";
}

public class ReportExportResultDto
{
    public byte[] FileBytes { get; set; } = [];
    public string ContentType { get; set; } = "text/csv";
    public string FileName { get; set; } = string.Empty;
}

