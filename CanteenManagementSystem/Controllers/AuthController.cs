using Canteen.BAL.Interfaces;
using Canteen.Model.DTOs;
using CanteenManagementSystem.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _env;

    public AuthController(IAuthService authService, IWebHostEnvironment env)
    {
        _authService = authService;
        _env = env;
    }

    /// <summary>Register a new canteen owner.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterOwnerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _authService.RegisterAsync(dto);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Log in an existing canteen owner and obtain a JWT token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _authService.LoginAsync(dto);
        if (response == null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(response);
    }

    /// <summary>Refresh the authenticated owner's JWT token to reflect current database state.</summary>
    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh()
    {
        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue)
        {
            return Unauthorized();
        }

        var response = await _authService.RefreshTokenAsync(ownerId.Value);
        if (response == null)
        {
            return Unauthorized();
        }

        return Ok(response);
    }

    /// <summary>Get the authenticated owner's current profile and canteen association from the database.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile()
    {
        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue)
        {
            return Unauthorized();
        }

        var response = await _authService.RefreshTokenAsync(ownerId.Value);
        if (response == null)
        {
            return Unauthorized();
        }

        return Ok(response);
    }

    /// <summary>Switch active canteen context for the authenticated owner.</summary>
    [HttpPost("switch-canteen/{canteenId:int}")]
    [Authorize(Roles = "CanteenOwner")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SwitchCanteen(int canteenId)
    {
        var ownerId = User.GetOwnerId();
        if (!ownerId.HasValue)
        {
            return Unauthorized();
        }

        var response = await _authService.SwitchCanteenAsync(ownerId.Value, canteenId);
        if (response == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not authorized to access this canteen." });
        }

        return Ok(response);
    }

    /// <summary>Request a password reset token.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ForgotPasswordResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var isDev = _env.IsDevelopment() || _env.IsEnvironment("Testing");
        var result = await _authService.ForgotPasswordAsync(dto, isDev);
        return Ok(result);
    }

    /// <summary>Reset password using a reset token.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var success = await _authService.ResetPasswordAsync(dto);
        if (!success)
        {
            return BadRequest(new { message = "Invalid, expired, or previously used password reset token." });
        }

        return Ok(new { message = "Password has been successfully reset. You can now log in with your new password." });
    }
}
