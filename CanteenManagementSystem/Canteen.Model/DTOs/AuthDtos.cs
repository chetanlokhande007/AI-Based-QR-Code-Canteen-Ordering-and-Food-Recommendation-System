using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs;

public class RegisterOwnerDto
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    public string Password { get; set; } = string.Empty;

    // Optional initial canteen setup fields from registration
    [StringLength(150)]
    public string? CanteenName { get; set; }

    [StringLength(30)]
    public string? ContactPhone { get; set; }

    [StringLength(250)]
    public string? CanteenAddress { get; set; }
}

public class LoginRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? CanteenId { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class ForgotPasswordRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordResponseDto
{
    public string Message { get; set; } = string.Empty;
    public string? ResetToken { get; set; }
}

public class ResetPasswordRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    public string NewPassword { get; set; } = string.Empty;
}
