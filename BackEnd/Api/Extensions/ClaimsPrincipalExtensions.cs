using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue) || !Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException("User identifier is missing or invalid.");
        }

        return userId;
    }
}
