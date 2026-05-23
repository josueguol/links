namespace Links.Api.Modules.Users;

// Reusing Auth.UserResponse is cleaner, but keeping module boundaries.
// This record can be removed if we decide to share DTOs across modules.
public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailVerified,
    bool MfaEnabled
);
