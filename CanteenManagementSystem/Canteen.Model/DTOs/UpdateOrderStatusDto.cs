using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs;

public class UpdateOrderStatusDto
{
    [Required(ErrorMessage = "Status is required.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Status must be between 3 and 30 characters.")]
    public string Status { get; set; } = string.Empty;
}
