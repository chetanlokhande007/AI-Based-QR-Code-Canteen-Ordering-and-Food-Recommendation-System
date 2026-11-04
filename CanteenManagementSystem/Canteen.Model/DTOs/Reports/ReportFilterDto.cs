using System;

namespace Canteen.Model.DTOs.Reports;

public class ReportFilterDto
{
    public int CanteenId { get; set; }

    /// <summary>
    /// Preset period: "today", "yesterday", "this_week", "last_week", "this_month", "last_month", "custom".
    /// Defaults to "this_month".
    /// </summary>
    public string Period { get; set; } = "this_month";

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; } = "desc";

    /// <summary>
    /// Resolves the current UTC date range and the preceding baseline comparison period of identical duration.
    /// </summary>
    public (DateTime StartUtc, DateTime EndUtc, DateTime PrevStartUtc, DateTime PrevEndUtc) GetUtcDateRanges()
    {
        var now = DateTime.UtcNow;
        var today = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);

        DateTime startUtc;
        DateTime endUtc;

        switch (Period?.ToLowerInvariant())
        {
            case "today":
                startUtc = today;
                endUtc = today.AddDays(1);
                break;

            case "yesterday":
                startUtc = today.AddDays(-1);
                endUtc = today;
                break;

            case "this_week":
                // Standard Monday start of week
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                startUtc = DateTime.SpecifyKind(today.AddDays(-1 * diff), DateTimeKind.Utc);
                endUtc = now;
                break;

            case "last_week":
                int lastWeekDiff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                var thisMonday = DateTime.SpecifyKind(today.AddDays(-1 * lastWeekDiff), DateTimeKind.Utc);
                startUtc = thisMonday.AddDays(-7);
                endUtc = thisMonday;
                break;

            case "last_month":
                var firstOfThisMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                startUtc = firstOfThisMonth.AddMonths(-1);
                endUtc = firstOfThisMonth;
                break;

            case "this_month":
                startUtc = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                endUtc = now;
                break;

            case "custom":
                startUtc = StartDate.HasValue 
                    ? DateTime.SpecifyKind(StartDate.Value.Date, DateTimeKind.Utc) 
                    : today.AddDays(-30);
                endUtc = EndDate.HasValue 
                    ? DateTime.SpecifyKind(EndDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc) 
                    : now;
                break;

            default:
                startUtc = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                endUtc = now;
                break;
        }

        if (endUtc <= startUtc)
        {
            endUtc = startUtc.AddDays(1);
        }

        var duration = endUtc - startUtc;
        var prevStartUtc = startUtc - duration;
        var prevEndUtc = startUtc;

        return (startUtc, endUtc, prevStartUtc, prevEndUtc);
    }
}

public class ReportPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

