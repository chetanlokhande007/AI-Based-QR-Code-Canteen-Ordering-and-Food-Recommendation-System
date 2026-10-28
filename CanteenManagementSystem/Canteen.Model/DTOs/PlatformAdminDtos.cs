using System;
using System.Collections.Generic;
using Canteen.Model.Enums;

namespace Canteen.Model.DTOs;

public class PlatformAdminMetricsDto
{
    public int TotalCanteens { get; set; }
    public int ActiveCanteens { get; set; }
    public int SuspendedCanteens { get; set; }
    public int TotalOwners { get; set; }
    public IEnumerable<AdminCanteenItemDto> RecentCanteens { get; set; } = new List<AdminCanteenItemDto>();
}

public class AdminCanteenItemDto
{
    public int CanteenId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? CanteenType { get; set; }
    public int? OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public TenantStatus TenantStatus { get; set; } = TenantStatus.Active;
    public SetupStatus SetupStatus { get; set; } = SetupStatus.Draft;
    public int MenuItemsCount { get; set; }
    public int TablesCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateCanteenStatusDto
{
    public bool? IsActive { get; set; }
    public TenantStatus? TenantStatus { get; set; }
}
