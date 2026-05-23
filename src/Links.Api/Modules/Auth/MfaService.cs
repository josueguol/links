using System.Security.Cryptography;
using System.Text;
using Links.Api.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using OtpNet;

namespace Links.Api.Modules.Auth;

public sealed class MfaService
{
    private readonly IUserRepository _repo;
    private readonly TokenService _tokens;
    private readonly IDataProtector _protector;
    private readonly IMemoryCache _cache;

    public MfaService(
        IUserRepository repo,
        TokenService tokens,
        IDataProtectionProvider dataProtection,
        IMemoryCache cache)
    {
        _repo = repo;
        _tokens = tokens;
        _protector = dataProtection.CreateProtector("Links.MfaSecret");
        _cache = cache;
    }

    public async Task<Result<MfaSetupResponse>> SetupMfaAsync(Guid userId)
    {
        var user = await _repo.GetByPublicIdAsync(userId);
        if (user is null)
            return new Error("USER_NOT_FOUND", "User not found.");

        if (user.MfaEnabled)
            return new Error("MFA_ALREADY_ENABLED", "MFA is already enabled.");

        var key = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(key);

        var encrypted = _protector.Protect(key);
        user.MfaSecret = Convert.ToBase64String(encrypted);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(user);

        var escapedEmail = Uri.EscapeDataString(user.Email);
        var uri = $"otpauth://totp/Links:{escapedEmail}?secret={secret}&issuer=Links&algorithm=SHA1&digits=6&period=30";

        return new MfaSetupResponse(secret, user.Email, "Links", uri);
    }

    public async Task<Result<MfaVerifyResponse>> VerifyMfaAsync(Guid userId, string code)
    {
        var user = await _repo.GetByPublicIdAsync(userId);
        if (user is null)
            return new Error("USER_NOT_FOUND", "User not found.");

        if (user.MfaEnabled)
            return new Error("MFA_ALREADY_ENABLED", "MFA is already enabled.");

        if (string.IsNullOrEmpty(user.MfaSecret))
            return new Error("MFA_NOT_SETUP", "MFA has not been set up yet.");

        var encrypted = Convert.FromBase64String(user.MfaSecret);
        var key = _protector.Unprotect(encrypted);

        var totp = new Totp(key, step: 30);
        if (!totp.VerifyTotp(code, out _, window: null))
            return new Error("INVALID_CODE", "Invalid verification code.");

        user.MfaEnabled = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(user);

        var backupCodes = GenerateBackupCodes();
        var now = DateTimeOffset.UtcNow;

        foreach (var plain in backupCodes)
        {
            var hash = HashCode(plain);
            await _repo.CreateBackupCodeAsync(new MfaBackupCode
            {
                UserId = user.Id,
                CodeHash = hash,
                CreatedAt = now
            });
        }

        return new MfaVerifyResponse(backupCodes);
    }

    // --- MFA login token (for second-factor authentication) ---

    public string CreateLoginToken(Guid userId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var key = HashToken(raw);
        _cache.Set(key, userId, TimeSpan.FromMinutes(5));
        return raw;
    }

    public Guid? ConsumeLoginToken(string token)
    {
        var key = HashToken(token);
        if (_cache.TryGetValue<Guid>(key, out var userId))
        {
            _cache.Remove(key);
            return userId;
        }
        return null;
    }

    // --- TOTP verification (without enabling MFA) ---

    public async Task<Result<User>> VerifyTotpCodeOnlyAsync(Guid userId, string code)
    {
        var user = await _repo.GetByPublicIdAsync(userId);
        if (user is null)
            return new Error("USER_NOT_FOUND", "User not found.");

        if (!user.MfaEnabled)
            return new Error("MFA_NOT_ENABLED", "MFA is not enabled for this user.");

        if (string.IsNullOrEmpty(user.MfaSecret))
            return new Error("MFA_NOT_SETUP", "MFA has not been set up yet.");

        var encrypted = Convert.FromBase64String(user.MfaSecret);
        var key = _protector.Unprotect(encrypted);

        var totp = new Totp(key, step: 30);
        if (!totp.VerifyTotp(code, out _, window: null))
            return new Error("INVALID_CODE", "Invalid verification code.");

        return user;
    }

    // --- Backup code verification ---

    public async Task<Result<User>> VerifyBackupCodeAsync(Guid publicId, string code)
    {
        var user = await _repo.GetByPublicIdAsync(publicId);
        if (user is null)
            return new Error("USER_NOT_FOUND", "User not found.");

        if (!user.MfaEnabled)
            return new Error("MFA_NOT_ENABLED", "MFA is not enabled for this user.");

        var codeHash = HashCode(code);
        var storedCodes = await _repo.GetBackupCodesByUserIdAsync(user.Id);

        for (var i = 0; i < storedCodes.Count; i++)
        {
            var stored = storedCodes[i];
            if (stored.CodeHash == codeHash && !stored.IsUsed)
            {
                await _repo.MarkBackupCodeUsedAsync(stored.Id);
                return user;
            }
        }

        return new Error("INVALID_BACKUP_CODE", "Invalid or already used backup code.");
    }

    // --- MFA authenticate (second factor) ---

    public async Task<Result<AuthResponse>> AuthenticateWithMfaAsync(string mfaToken, string code)
    {
        var userId = ConsumeLoginToken(mfaToken);
        if (userId is null)
            return new Error("INVALID_MFA_TOKEN", "Invalid or expired MFA token.");

        // Try TOTP first
        var totpResult = await VerifyTotpCodeOnlyAsync(userId.Value, code);
        if (totpResult.IsSuccess)
            return await CreateAuthResponseAsync(totpResult.Value!);

        // If TOTP fails, try backup code
        if (totpResult.Error?.Code == "INVALID_CODE")
        {
            var backupResult = await VerifyBackupCodeAsync(userId.Value, code);
            if (backupResult.IsSuccess)
                return await CreateAuthResponseAsync(backupResult.Value!);
        }

        return totpResult.Error!.Value;
    }

    public async Task<AuthResponse> CreateAuthResponseAsync(User user)
    {
        var refreshToken = _tokens.GenerateRefreshToken();
        var family = Guid.NewGuid();

        await _repo.CreateRefreshTokenAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokens.HashToken(refreshToken),
            Family = family,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow
        });

        return new AuthResponse(
            _tokens.GenerateAccessToken(user),
            refreshToken,
            MapUser(user));
    }

    private static UserResponse MapUser(User user) => new(
        user.PublicId,
        user.Email,
        user.DisplayName,
        user.EmailVerifiedAt is not null,
        user.MfaEnabled);

    private static string[] GenerateBackupCodes()
    {
        const int count = 8;
        const int length = 8;
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var codes = new string[count];

        for (var i = 0; i < count; i++)
        {
            var bytes = RandomNumberGenerator.GetBytes(length);
            var code = new char[length];
            for (var j = 0; j < length; j++)
                code[j] = chars[bytes[j] % chars.Length];
            codes[i] = new string(code);
        }

        return codes;
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }
}
