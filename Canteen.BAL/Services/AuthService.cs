using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Canteen.BAL.Interfaces;
using Canteen.DAL.Interfaces;
using Canteen.Model.DTOs;
using Canteen.Model.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Canteen.BAL.Services;

public class AuthService : IAuthService
{
    private readonly IOwnerRepository _ownerRepository;
    private readonly ICanteenRepository _canteenRepository;
    private readonly IConfiguration _configuration;

    public AuthService(
        IOwnerRepository ownerRepository,
        ICanteenRepository canteenRepository,
        IConfiguration configuration)
    {
        _ownerRepository = ownerRepository;
        _canteenRepository = canteenRepository;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterOwnerDto dto)
    {
        if (await _ownerRepository.EmailExistsAsync(dto.Email))
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var owner = new Owner
        {
            FullName = dto.FullName.Trim(),
            Email = dto.Email.Trim().ToLower(),
            PasswordHash = HashPassword(dto.Password),
            Role = "CanteenOwner",
            CanteenId = null,
            CreatedAt = DateTime.UtcNow
        };

        var createdOwner = await _ownerRepository.AddAsync(owner);
        var token = GenerateJwtToken(createdOwner);

        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var exp) ? exp : 30;

        return new AuthResponseDto
        {
            Token = token,
            OwnerId = createdOwner.OwnerId,
            FullName = createdOwner.FullName,
            Email = createdOwner.Email,
            Role = createdOwner.Role,
            CanteenId = createdOwner.CanteenId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto dto)
    {
        var owner = await _ownerRepository.GetByEmailAsync(dto.Email);
        if (owner == null)
        {
            return null;
        }

        if (!VerifyPassword(dto.Password, owner.PasswordHash))
        {
            return null;
        }

        var token = GenerateJwtToken(owner);
        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var exp) ? exp : 30;

        return new AuthResponseDto
        {
            Token = token,
            OwnerId = owner.OwnerId,
            FullName = owner.FullName,
            Email = owner.Email,
            Role = owner.Role,
            CanteenId = owner.CanteenId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };
    }

    private string GenerateJwtToken(Owner owner)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT key is missing.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("JWT issuer is missing.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("JWT audience is missing.");

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(jwtKey);
        }
        catch
        {
            keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        }

        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("JWT key must contain at least 32 bytes (256 bits).");
        }

        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var exp) ? exp : 30;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, owner.OwnerId.ToString()),
            new(ClaimTypes.Email, owner.Email),
            new(ClaimTypes.Name, owner.FullName),
            new(ClaimTypes.Role, owner.Role)
        };

        if (owner.CanteenId.HasValue)
        {
            claims.Add(new Claim("CanteenId", owner.CanteenId.Value.ToString()));
        }

        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    // ─── Cryptographic Password Hashing (PBKDF2-HMAC-SHA256) ───────────────────
    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: 100_000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 2) return false;

        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] originalHash = Convert.FromBase64String(parts[1]);

        byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: 100_000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);

        return CryptographicOperations.FixedTimeEquals(originalHash, computedHash);
    }
}
