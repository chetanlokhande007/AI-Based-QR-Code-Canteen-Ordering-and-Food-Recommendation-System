using System;
using System.ComponentModel.DataAnnotations;

namespace Canteen.Model.DTOs;

public class CreateTvPairingRequestDto
{
    [MaxLength(50)]
    public string? DeviceName { get; set; }

    [MaxLength(20)]
    public string StationScope { get; set; } = "All"; // "All", "Food", "Beverage"
}

public class TvPairingSessionResponseDto
{
    public int SessionId { get; set; }
    public int CanteenId { get; set; }
    public string PairingCode { get; set; } = string.Empty;
    public string StationScope { get; set; } = "All";
    public DateTime ExpiresAt { get; set; }
    public int ExpiresInSeconds { get; set; }
}

public class ClaimTvPairingDto
{
    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Pairing code must be a 6-digit numeric PIN.")]
    public string PairingCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DeviceIdentifier { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? DeviceName { get; set; }
}

public class TvClaimResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? DeviceToken { get; set; }
    public int CanteenId { get; set; }
    public string CanteenName { get; set; } = string.Empty;
    public int TvDeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string StationScope { get; set; } = "All";
}

public class TvDeviceDto
{
    public int TvDeviceId { get; set; }
    public int CanteenId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceIdentifier { get; set; } = string.Empty;
    public string StationScope { get; set; } = "All";
    public bool IsActive { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public class UpdateTvDeviceDto
{
    [Required]
    [MaxLength(50)]
    public string DeviceName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string StationScope { get; set; } = "All"; // "All", "Food", "Beverage"
}

