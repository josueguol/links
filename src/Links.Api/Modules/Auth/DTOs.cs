namespace Links.Api.Modules.Auth;

// --- Requests ---

public sealed record RegisterRequest(string Email, string Password, string DisplayName);

public sealed record VerifyEmailRequest(string Token);

public sealed record LoginRequest(string Email, string Password);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

// --- Responses ---

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    UserResponse User
);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailVerified,
    bool MfaEnabled
);

// --- MFA ---

public sealed record MfaSetupResponse(
    string Secret,
    string Account,
    string Issuer,
    string OtpAuthUri
);

public sealed record MfaVerifyRequest(string Code);

public sealed record MfaVerifyResponse(string[] BackupCodes);

public sealed record MfaAuthenticateRequest(string MfaToken, string Code);
