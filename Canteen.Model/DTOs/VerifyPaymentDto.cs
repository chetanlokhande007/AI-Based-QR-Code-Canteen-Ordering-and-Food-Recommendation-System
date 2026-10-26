using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs;

public class VerifyPaymentDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 5)]
    public string RazorpayOrderId { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 5)]
    public string RazorpayPaymentId { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 10)]
    public string RazorpaySignature { get; set; } = string.Empty;
}
