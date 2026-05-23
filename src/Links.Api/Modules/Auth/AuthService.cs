using Links.Api.Common;

namespace Links.Api.Modules.Auth;

public sealed class AuthService
{
    private readonly IUserRepository _repo;
    private readonly PasswordHasher _hasher;
    private readonly TokenService _tokens;
    private readonly IEmailSender _email;
    private readonly MfaService _mfa;

    public AuthService(
        IUserRepository repo,
        PasswordHasher hasher,
        TokenService tokens,
        IEmailSender email,
        MfaService mfa)
    {
        _repo = repo;
        _hasher = hasher;
        _tokens = tokens;
        _email = email;
        _mfa = mfa;
    }

    // --- Register ---

    public async Task<Result<SuccessResponse>> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existing = await _repo.GetByEmailAsync(email);
        if (existing is not null)
            return new Error("EMAIL_EXISTS", "Email is already registered.");

        var now = DateTimeOffset.UtcNow;
        var userId = await _repo.CreateAsync(new User
        {
            PublicId = Guid.NewGuid(),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        });

        var verToken = _tokens.GenerateEmailVerificationToken();

        await _repo.CreateVerificationTokenAsync(new EmailVerificationToken
        {
            UserId = userId,
            TokenHash = _tokens.HashToken(verToken),
            ExpiresAt = now.AddHours(24),
            CreatedAt = now
        });

        await _email.SendVerificationEmailAsync(email, verToken);

        return new SuccessResponse("Registration successful. Please check your email to verify your account.");
    }

    // --- Verify email ---

    public async Task<Result<SuccessResponse>> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var tokenHash = _tokens.HashToken(request.Token);
        var stored = await _repo.GetVerificationTokenByHashAsync(tokenHash);

        if (stored is null)
            return new Error("INVALID_TOKEN", "Invalid verification token.");

        if (stored.UsedAt is not null)
            return new Error("TOKEN_USED", "Token has already been used.");

        if (stored.ExpiresAt < DateTimeOffset.UtcNow)
            return new Error("TOKEN_EXPIRED", "Verification token has expired.");

        var user = await _repo.GetByIdAsync(stored.UserId);
        if (user is null)
            return new Error("USER_NOT_FOUND", "User not found.");

        user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(user);

        await _repo.MarkVerificationTokenUsedAsync(stored.Id);

        return new SuccessResponse("Email verified successfully.");
    }

    // --- Login ---

    public async Task<Result<LoginResult>> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _repo.GetByEmailAsync(email);

        if (user is null)
            return new Error("INVALID_CREDENTIALS", "Invalid email or password.");

        if (!_hasher.Verify(request.Password, user.PasswordHash))
            return new Error("INVALID_CREDENTIALS", "Invalid email or password.");

        if (user.EmailVerifiedAt is null)
            return new Error("EMAIL_NOT_VERIFIED", "Please verify your email before logging in.");

        if (user.MfaEnabled)
        {
            var mfaToken = _mfa.CreateLoginToken(user.PublicId);
            return new LoginResult(
                AccessToken: null,
                RefreshToken: null,
                User: null,
                MfaRequired: true,
                MfaToken: mfaToken);
        }

        var auth = await _mfa.CreateAuthResponseAsync(user);
        return new LoginResult(
            auth.AccessToken,
            auth.RefreshToken,
            auth.User,
            MfaRequired: false,
            MfaToken: null);
    }

    // --- Refresh token ---

    public async Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken)
    {
        var tokenHash = _tokens.HashToken(refreshToken);
        var stored = await _repo.GetRefreshTokenByHashAsync(tokenHash);

        if (stored is null)
            return new Error("INVALID_TOKEN", "Refresh token not found.");

        if (stored.IsUsed)
        {
            await _repo.RevokeRefreshTokenFamilyAsync(stored.Family, stored.UserId);
            return new Error("TOKEN_REUSED", "Refresh token has already been used. All sessions revoked.");
        }

        if (stored.ExpiresAt < DateTimeOffset.UtcNow)
            return new Error("TOKEN_EXPIRED", "Refresh token has expired.");

        var user = await _repo.GetByIdAsync(stored.UserId);
        if (user is null)
            return new Error("USER_NOT_FOUND", "User not found.");

        await _repo.MarkRefreshTokenUsedAsync(stored.Id);

        var newRefreshToken = _tokens.GenerateRefreshToken();
        await _repo.CreateRefreshTokenAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokens.HashToken(newRefreshToken),
            Family = stored.Family,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow
        });

        return new AuthResponse(
            _tokens.GenerateAccessToken(user),
            newRefreshToken,
            MapUser(user));
    }

    // --- Logout ---

    public async Task LogoutAsync(string refreshToken)
    {
        var tokenHash = _tokens.HashToken(refreshToken);
        var stored = await _repo.GetRefreshTokenByHashAsync(tokenHash);

        if (stored is not null)
        {
            await _repo.MarkRefreshTokenUsedAsync(stored.Id);
        }
    }

    // --- Forgot password ---

    public async Task<Result<SuccessResponse>> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _repo.GetByEmailAsync(email);

        // Always return same message to avoid revealing email existence
        var genericMessage = "If the email exists, a password reset link has been sent.";

        if (user is null)
            return new SuccessResponse(genericMessage);

        var resetToken = _tokens.GenerateEmailVerificationToken();

        await _repo.CreatePasswordResetTokenAsync(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = _tokens.HashToken(resetToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _email.SendPasswordResetEmailAsync(email, resetToken);

        return new SuccessResponse(genericMessage);
    }

    private static UserResponse MapUser(User user) => new(
        user.PublicId,
        user.Email,
        user.DisplayName,
        user.EmailVerifiedAt is not null,
        user.MfaEnabled);

    // --- Reset password ---

    public async Task<Result<SuccessResponse>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var tokenHash = _tokens.HashToken(request.Token);
        var stored = await _repo.GetPasswordResetTokenByHashAsync(tokenHash);

        if (stored is null)
            return new Error("INVALID_TOKEN", "Invalid reset token.");

        if (stored.UsedAt is not null)
            return new Error("TOKEN_USED", "Token has already been used.");

        if (stored.ExpiresAt < DateTimeOffset.UtcNow)
            return new Error("TOKEN_EXPIRED", "Reset token has expired.");

        var user = await _repo.GetByIdAsync(stored.UserId);
        if (user is null)
            return new Error("USER_NOT_FOUND", "User not found.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(user);

        await _repo.MarkPasswordResetTokenUsedAsync(stored.Id);

        // Revoke all sessions — user must log in again
        await _repo.RevokeUserRefreshTokensAsync(user.Id);

        return new SuccessResponse("Password has been reset successfully.");
    }
}
