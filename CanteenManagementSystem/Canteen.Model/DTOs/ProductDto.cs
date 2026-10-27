using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs
{
    public class ProductDto
    {
        public int ProductId { get; set; }
        public int CanteenId { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsAvailable { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateProductDto
    {
        public int CanteenId { get; set; }

        [Required(ErrorMessage = "CategoryId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "A valid CategoryId is required.")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Product name must be between 2 and 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required.")]
        [Range(0.01, 100000.00, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }

        [Range(0, 100000, ErrorMessage = "Stock quantity cannot be negative.")]
        public int StockQuantity { get; set; }

        [Url(ErrorMessage = "ImageUrl must be a valid URL.")]
        [StringLength(1000, ErrorMessage = "ImageUrl cannot exceed 1000 characters.")]
        public string? ImageUrl { get; set; }
    }

    public class UpdateProductDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "A valid CategoryId is required.")]
        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Product name must be between 2 and 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required.")]
        [Range(0.01, 100000.00, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }

        [Range(0, 100000, ErrorMessage = "Stock quantity cannot be negative.")]
        public int StockQuantity { get; set; }

        public bool IsAvailable { get; set; }

        [Url(ErrorMessage = "ImageUrl must be a valid URL.")]
        [StringLength(1000, ErrorMessage = "ImageUrl cannot exceed 1000 characters.")]
        public string? ImageUrl { get; set; }
    }
}
