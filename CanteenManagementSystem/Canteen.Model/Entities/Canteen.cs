using System;
using System.Collections.Generic;

namespace Canteen.Model.Entities
{
    public class Canteen
    {
        public int CanteenId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        // Additional Canteen Profile Details
        public string? CanteenType { get; set; }
        public string? Description { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }

        // Structured Address Fields
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? Landmark { get; set; }

        // Store / Operating Hours JSON
        public string? OperatingHoursJson { get; set; }

        // Canteen Banner / Photo
        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? OwnerId { get; set; }
        public Owner? Owner { get; set; }

        public ICollection<CanteenTable> Tables { get; set; }
            = new List<CanteenTable>();

        public ICollection<Product> Products { get; set; }
            = new List<Product>();
    }
}
