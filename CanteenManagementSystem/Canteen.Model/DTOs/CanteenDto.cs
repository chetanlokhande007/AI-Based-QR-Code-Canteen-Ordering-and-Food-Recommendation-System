using System;
using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs
{
    public class CanteenDto
    {
        public int CanteenId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        public string? CanteenType { get; set; }
        public string? Description { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? Landmark { get; set; }

        public string? OperatingHoursJson { get; set; }
        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Token { get; set; }
        public int? OwnerId { get; set; }
    }

    public class CanteenSummaryDto
    {
        public int CanteenId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string Status { get; set; } = "Active"; // "Active", "Setup Pending", "Inactive"
        public int MenuItemsCount { get; set; }
        public int TablesCount { get; set; }
        public int TodayOrdersCount { get; set; }
        public bool IsActive { get; set; }
        public bool IsSetupComplete { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateCanteenDto
    {
        [Required(ErrorMessage = "Canteen name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Canteen name must be between 2 and 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Location is required.")]
        [StringLength(250, MinimumLength = 2, ErrorMessage = "Location must be between 2 and 250 characters.")]
        public string Location { get; set; } = string.Empty;

        public string? CanteenType { get; set; }
        public string? Description { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? Landmark { get; set; }

        public string? OperatingHoursJson { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class UpdateCanteenDto
    {
        [Required(ErrorMessage = "Canteen name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Canteen name must be between 2 and 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Location is required.")]
        [StringLength(250, MinimumLength = 2, ErrorMessage = "Location must be between 2 and 250 characters.")]
        public string Location { get; set; } = string.Empty;

        public string? CanteenType { get; set; }
        public string? Description { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? Landmark { get; set; }

        public string? OperatingHoursJson { get; set; }
        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; }
    }
}
