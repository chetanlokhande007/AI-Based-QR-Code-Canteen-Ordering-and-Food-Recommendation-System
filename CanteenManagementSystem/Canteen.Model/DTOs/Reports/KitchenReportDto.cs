using System;
using System.Collections.Generic;

namespace Canteen.Model.DTOs.Reports;

public class KitchenReportDto
{
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Aggregate Kitchen Metrics
    public int TotalOrdersProcessed { get; set; }
    public int TotalDishesPrepared { get; set; }
    public decimal AveragePreparationMinutes { get; set; }
    public int OrdersPreparedUnder15Minutes { get; set; }
    public int DelayedOrdersCount { get; set; } // > 20 mins wait
    public decimal KitchenEfficiencyScore { get; set; } // % orders on-time

    // Kitchen Velocity by Time of Day
    public List<HourlyKitchenVelocityDto> VelocityByHour { get; set; } = new();

    // Top Cooked Items Throughput
    public List<KitchenDishThroughputDto> TopPreparedDishes { get; set; } = new();

    // Paginated Detailed Order Kitchen Logs
    public ReportPagedResult<KitchenOrderQueueItemDto> OrderQueue { get; set; } = new();
}

public class HourlyKitchenVelocityDto
{
    public int Hour { get; set; }
    public string HourLabel { get; set; } = string.Empty;
    public int OrdersCount { get; set; }
    public decimal AvgPrepMinutes { get; set; }
}

public class KitchenDishThroughputDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int TotalUnitsCooked { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class KitchenOrderQueueItemDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public int TotalItemsCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsDelayed { get; set; }
}

