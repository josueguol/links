using System.Security.Claims;

namespace Links.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (value is null)
            return null;

        return Guid.TryParse(value, out var id) ? id : null;
    }
}
