using System.Security.Claims;
using Links.Api.Common;
using Links.Api.Modules.Auth;

namespace Links.Api.Modules.Users;

public sealed class UserService
{
    private readonly IUserRepository _repo;

    public UserService(IUserRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<UserResponse>> GetMyProfileAsync(Guid publicId)
    {
        var user = await _repo.GetByPublicIdAsync(publicId);
        if (user is null)
            return new Error("USER_NOT_FOUND", "User not found.");

        return new UserResponse(
            user.PublicId,
            user.Email,
            user.DisplayName,
            user.EmailVerifiedAt is not null,
            user.MfaEnabled);
    }

    public static Guid GetCurrentUserId(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID not found in token.");
        return Guid.Parse(id);
    }
}
