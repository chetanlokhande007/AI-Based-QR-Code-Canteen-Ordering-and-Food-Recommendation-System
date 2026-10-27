using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs
{
    public class CanteenTableDto
    {
        public int TableId { get; set; }
        public int CanteenId { get; set; }
        public int TableNumber { get; set; }
        public int Capacity { get; set; }
        public string QRToken { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }

    public class CreateCanteenTableDto
    {
        public int CanteenId { get; set; }

        [Required(ErrorMessage = "Table number is required.")]
        [Range(1, 10000, ErrorMessage = "Table number must be between 1 and 10000.")]
        public int TableNumber { get; set; }

        [Required(ErrorMessage = "Capacity is required.")]
        [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100.")]
        public int Capacity { get; set; }
    }

    public class UpdateCanteenTableDto
    {
        [Required(ErrorMessage = "Table number is required.")]
        [Range(1, 10000, ErrorMessage = "Table number must be between 1 and 10000.")]
        public int TableNumber { get; set; }

        [Required(ErrorMessage = "Capacity is required.")]
        [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100.")]
        public int Capacity { get; set; }

        public bool IsAvailable { get; set; }
    }
}
