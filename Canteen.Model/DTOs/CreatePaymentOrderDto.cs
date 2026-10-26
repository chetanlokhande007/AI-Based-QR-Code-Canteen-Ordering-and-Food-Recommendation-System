using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs;

public class CreatePaymentOrderDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "A valid OrderId is required.")]
    public int OrderId { get; set; }
}
