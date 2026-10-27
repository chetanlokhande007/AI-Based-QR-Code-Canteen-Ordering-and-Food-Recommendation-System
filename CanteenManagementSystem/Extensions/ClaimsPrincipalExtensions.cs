using System.Security.Claims;

namespace CanteenManagementSystem.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Safely extracts the CanteenId claim from the authenticated user's JWT.
    /// </summary>
    public static int? GetCanteenId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("CanteenId")?.Value;
        return int.TryParse(claim, out var canteenId) ? canteenId : null;
    }

    /// <summary>
    /// Safely extracts the OwnerId (NameIdentifier) from the authenticated user's JWT.
    /// </summary>
    public static int? GetOwnerId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var ownerId) ? ownerId : null;
    }
}
