using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs;

public class CreateOrderDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int TableId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "An order must contain at least one item.")]
    public List<CreateOrderItemDto> Items { get; set; } = new();

    /// <summary>
    /// Optional promotional coupon code entered at checkout (e.g. "WELCOME20").
    /// </summary>
    public string? CouponCode { get; set; }

    /// <summary>
    /// Optional customer identifier (e.g. phone number) for usage tracking.
    /// </summary>
    public string? CustomerPhone { get; set; }
}

public class CreateOrderItemDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}
