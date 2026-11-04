using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs.Reports;

public class PeakHourReportDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Summary Peak Insights
    public string BusiestHourLabel { get; set; } = string.Empty;
    public int BusiestHourOrdersCount { get; set; }
    public decimal BusiestHourSales { get; set; }
    public string BusiestDayOfWeek { get; set; } = string.Empty;
    public decimal AverageOrdersPerHour { get; set; }
    public decimal PeakHourRevenueContribution { get; set; } // %

    // 24-Hour Traffic Heatmap Data
    public List<HourlyPeakTrafficStatDto> HourlyTraffic { get; set; } = new();

    // Day of the Week Distribution
    public List<DayOfWeekPeakStatDto> DayOfWeekTraffic { get; set; } = new();

    // Table Utilization Peaks
    public List<TableUtilizationPeakDto> TableUtilization { get; set; } = new();
}

public class HourlyPeakTrafficStatDto
{
    public int Hour { get; set; } // 0 - 23
    public string HourLabel { get; set; } = string.Empty; // e.g. "12 PM"
    public int OrdersCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal TrafficPercentage { get; set; }
    public bool IsPeakHour { get; set; }
}

public class DayOfWeekPeakStatDto
{
    public int DayOfWeekIndex { get; set; } // 0 = Sunday, 1 = Monday, ...
    public string DayName { get; set; } = string.Empty;
    public int OrdersCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class TableUtilizationPeakDto
{
    public int TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public int TotalOrdersServed { get; set; }
    public decimal TotalRevenueGenerated { get; set; }
    public decimal UtilizationPercentage { get; set; }
}

