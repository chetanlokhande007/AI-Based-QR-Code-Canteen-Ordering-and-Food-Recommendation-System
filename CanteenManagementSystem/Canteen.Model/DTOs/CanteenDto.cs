using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs
{
    public class CanteenDto
    {
        public int CanteenId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Token { get; set; }
    }

    public class CreateCanteenDto
    {
        [Required(ErrorMessage = "Canteen name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Canteen name must be between 2 and 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Location is required.")]
        [StringLength(250, MinimumLength = 2, ErrorMessage = "Location must be between 2 and 250 characters.")]
        public string Location { get; set; } = string.Empty;
    }

    public class UpdateCanteenDto
    {
        [Required(ErrorMessage = "Canteen name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Canteen name must be between 2 and 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Location is required.")]
        [StringLength(250, MinimumLength = 2, ErrorMessage = "Location must be between 2 and 250 characters.")]
        public string Location { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
