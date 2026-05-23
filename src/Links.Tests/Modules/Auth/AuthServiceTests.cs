using Links.Api.Common;
using Links.Api.Modules.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Links.Tests.Modules.Auth;

public sealed class AuthServiceTests : IDisposable
{
    private readonly AuthService _sut;
    private readonly MfaService _mfa;
    private readonly FakeUserRepository _repo;
    private readonly FakeEmailSender _email;
    private readonly MemoryCache _cache;

    public AuthServiceTests()
    {
        _repo = new FakeUserRepository();
        _email = new FakeEmailSender();

        var hasher = new PasswordHasher();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Jwt:Secret",
                    "test-secret-key-at-least-32-chars-long-for-hs256!")
            })
            .Build();

        var tokens = new TokenService(config);
        var dataProtection = new FakeDataProtectionProvider();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mfa = new MfaService(_repo, tokens, dataProtection, _cache);
        _sut = new AuthService(_repo, hasher, tokens, _email, _mfa);
    }

    public void Dispose()
    {
        _repo.Clear();
        _cache.Dispose();
    }

    // --- Register ---

    [Fact]
    public async Task Register_WithValidData_ReturnsSuccess()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest(
            "test@example.com", "SecurePass1", "Test User"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Contains("success", result.Value.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsError()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "dup@example.com", "SecurePass1", "First"));

        var result = await _sut.RegisterAsync(new RegisterRequest(
            "dup@example.com", "SecurePass1", "Second"));

        Assert.True(result.IsFailure);
        Assert.Equal("EMAIL_EXISTS", result.Error?.Code);
    }

    [Fact]
    public async Task Register_TrimsAndLowercasesEmail()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest(
            "  Test@Example.COM  ", "SecurePass1", "User"));

        Assert.True(result.IsSuccess);

        var user = await _repo.GetByEmailAsync("test@example.com");
        Assert.NotNull(user);
        Assert.Equal("test@example.com", user!.Email);
    }

    [Fact]
    public async Task Register_SendsVerificationEmail()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "verify@example.com", "SecurePass1", "User"));

        Assert.Single(_email.SentMessages);
        Assert.Equal("verify@example.com", _email.SentMessages[0].Email);
    }

    [Fact]
    public async Task Register_DoesNotReturnTokens()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest(
            "notokens@example.com", "SecurePass1", "User"));

        Assert.True(result.IsSuccess);
        Assert.IsType<SuccessResponse>(result.Value);
    }

    // --- Verify Email ---

    [Fact]
    public async Task VerifyEmail_WithValidToken_VerifiesUser()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "verifyme@example.com", "SecurePass1", "User"));

        var token = _email.SentMessages[0].Token;

        var result = await _sut.VerifyEmailAsync(new VerifyEmailRequest(token));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_ReturnsError()
    {
        var result = await _sut.VerifyEmailAsync(
            new VerifyEmailRequest("invalid-token"));

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TOKEN", result.Error?.Code);
    }

    [Fact]
    public async Task VerifyEmail_WithExpiredToken_ReturnsError()
    {
        var token = "expired-test-token";
        await _repo.CreateVerificationTokenAsync(new EmailVerificationToken
        {
            UserId = 1,
            TokenHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(token))),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        });

        var result = await _sut.VerifyEmailAsync(new VerifyEmailRequest(token));

        Assert.True(result.IsFailure);
        Assert.Equal("TOKEN_EXPIRED", result.Error?.Code);
    }

    // --- Login ---

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "login@example.com", "SecurePass1", "User"));

        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var result = await _sut.LoginAsync(new LoginRequest(
            "login@example.com", "SecurePass1"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.AccessToken);
        Assert.NotNull(result.Value.RefreshToken);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsError()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "wrongpass@example.com", "SecurePass1", "User"));

        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var result = await _sut.LoginAsync(new LoginRequest(
            "wrongpass@example.com", "WrongPass1"));

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_CREDENTIALS", result.Error?.Code);
    }

    [Fact]
    public async Task Login_WithUnverifiedEmail_ReturnsError()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "unverified@example.com", "SecurePass1", "User"));

        var result = await _sut.LoginAsync(new LoginRequest(
            "unverified@example.com", "SecurePass1"));

        Assert.True(result.IsFailure);
        Assert.Equal("EMAIL_NOT_VERIFIED", result.Error?.Code);
    }

    [Fact]
    public async Task Login_WithNonexistentEmail_ReturnsError()
    {
        var result = await _sut.LoginAsync(new LoginRequest(
            "nobody@example.com", "SecurePass1"));

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_CREDENTIALS", result.Error?.Code);
    }

    // --- Refresh Token ---

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "refresh@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "refresh@example.com", "SecurePass1"));

        Assert.NotNull(loginResult.Value);
        var refreshToken = loginResult.Value.RefreshToken;
        var originalAccessToken = loginResult.Value.AccessToken;

        var result = await _sut.RefreshTokenAsync(refreshToken!);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEqual(originalAccessToken, result.Value.AccessToken);
        Assert.NotEqual(refreshToken, result.Value.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_WithReusedToken_RevokesFamily()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "reuse@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "reuse@example.com", "SecurePass1"));

        Assert.NotNull(loginResult.Value);
        var refreshToken = loginResult.Value.RefreshToken;

        await _sut.RefreshTokenAsync(refreshToken!);

        var result = await _sut.RefreshTokenAsync(refreshToken!);

        Assert.True(result.IsFailure);
        Assert.Equal("TOKEN_REUSED", result.Error?.Code);
    }

    // --- Logout ---

    [Fact]
    public async Task Logout_WithValidToken_TokenIsRevoked()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "logout@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "logout@example.com", "SecurePass1"));

        Assert.NotNull(loginResult.Value);
        var refreshToken = loginResult.Value.RefreshToken;

        await _sut.LogoutAsync(refreshToken!);

        var result = await _sut.RefreshTokenAsync(refreshToken!);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Logout_WithUnknownToken_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(
            () => _sut.LogoutAsync("nonexistent-token"));

        Assert.Null(exception);
    }

    // --- Forgot Password ---

    [Fact]
    public async Task ForgotPassword_WithExistingEmail_ReturnsSuccess()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "forgot@example.com", "SecurePass1", "User"));

        var result = await _sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("forgot@example.com"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task ForgotPassword_WithNonexistentEmail_ReturnsSuccess()
    {
        var result = await _sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("nobody@example.com"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task ForgotPassword_DoesNotRevealEmailExistence()
    {
        // Known existing user
        await _sut.RegisterAsync(new RegisterRequest(
            "exists@example.com", "SecurePass1", "User"));

        var resultKnown = await _sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("exists@example.com"));

        // Non-existing user
        var resultUnknown = await _sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("nobody@example.com"));

        Assert.Equal(resultKnown.Value?.Message, resultUnknown.Value?.Message);
    }

    [Fact]
    public async Task ForgotPassword_SendsEmailWithToken()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "sendreset@example.com", "SecurePass1", "User"));

        var beforeCount = _email.SentMessages.Count;
        await _sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("sendreset@example.com"));

        var newMessages = _email.SentMessages.Skip(beforeCount).ToList();
        Assert.Single(newMessages);
        Assert.Equal("sendreset@example.com", newMessages[0].Email);
    }

    // --- Reset Password ---

    [Fact]
    public async Task ResetPassword_WithValidToken_ChangesPassword()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "reset@example.com", "SecurePass1", "User"));

        // Request reset
        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest("reset@example.com"));
        var resetToken = _email.SentMessages.Last().Token;

        // Reset with new password
        var result = await _sut.ResetPasswordAsync(
            new ResetPasswordRequest(resetToken, "NewSecure1"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsError()
    {
        var result = await _sut.ResetPasswordAsync(
            new ResetPasswordRequest("bad-token", "NewSecure1"));

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TOKEN", result.Error?.Code);
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_ReturnsError()
    {
        var token = "expired-reset-token";
        await _repo.CreatePasswordResetTokenAsync(new PasswordResetToken
        {
            UserId = 1,
            TokenHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(token))),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        var result = await _sut.ResetPasswordAsync(
            new ResetPasswordRequest(token, "NewSecure1"));

        Assert.True(result.IsFailure);
        Assert.Equal("TOKEN_EXPIRED", result.Error?.Code);
    }

    [Fact]
    public async Task ResetPassword_WithUsedToken_ReturnsError()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "usedtoken@example.com", "SecurePass1", "User"));

        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest("usedtoken@example.com"));
        var resetToken = _email.SentMessages.Last().Token;

        // First use — should succeed
        await _sut.ResetPasswordAsync(new ResetPasswordRequest(resetToken, "NewSecure1"));

        // Second use with same token — should fail
        var result = await _sut.ResetPasswordAsync(
            new ResetPasswordRequest(resetToken, "Another1"));

        Assert.True(result.IsFailure);
        Assert.Equal("TOKEN_USED", result.Error?.Code);
    }

    [Fact]
    public async Task ResetPassword_RevokesActiveSessions()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "revokesessions@example.com", "SecurePass1", "User"));

        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        // Login to create active refresh tokens
        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "revokesessions@example.com", "SecurePass1"));

        Assert.NotNull(loginResult.Value);
        var sessionRefreshToken = loginResult.Value.RefreshToken;

        // Request password reset
        await _sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("revokesessions@example.com"));
        var resetToken = _email.SentMessages.Last().Token;

        // Reset password
        await _sut.ResetPasswordAsync(new ResetPasswordRequest(resetToken, "NewSecure1"));

        // Old session refresh token should now be revoked
        var oldSessionResult = await _sut.RefreshTokenAsync(sessionRefreshToken!);
        Assert.True(oldSessionResult.IsFailure);
    }

    [Fact]
    public async Task ResetPassword_AllowsLoginWithNewPassword()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "newpasslogin@example.com", "SecurePass1", "User"));

        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        await _sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("newpasslogin@example.com"));
        var resetToken = _email.SentMessages.Last().Token;

        await _sut.ResetPasswordAsync(new ResetPasswordRequest(resetToken, "NewSecure1"));

        // Login with new password should work
        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "newpasslogin@example.com", "NewSecure1"));

        Assert.True(loginResult.IsSuccess);
    }

    [Fact]
    public async Task ResetPassword_WithOldPassword_Fails()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "oldpassfail@example.com", "SecurePass1", "User"));

        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        await _sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("oldpassfail@example.com"));
        var resetToken = _email.SentMessages.Last().Token;

        await _sut.ResetPasswordAsync(new ResetPasswordRequest(resetToken, "NewSecure1"));

        // Login with old password should fail
        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "oldpassfail@example.com", "SecurePass1"));

        Assert.True(loginResult.IsFailure);
        Assert.Equal("INVALID_CREDENTIALS", loginResult.Error?.Code);
    }

    // --- MFA ---

    [Fact]
    public async Task MfaSetup_GeneratesSecret_DoesNotEnableMfaYet()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "mfa@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "mfa@example.com", "SecurePass1"));

        Assert.NotNull(loginResult.Value);
        Assert.NotNull(loginResult.Value.User);
        var userId = loginResult.Value.User.Id;

        var result = await _mfa.SetupMfaAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.Secret);
        Assert.Contains("otpauth://", result.Value.OtpAuthUri);
        Assert.Equal("mfa@example.com", result.Value.Account);
        Assert.Equal("Links", result.Value.Issuer);

        // MFA should not be enabled yet
        var user = await _repo.GetByPublicIdAsync(userId);
        Assert.NotNull(user);
        Assert.False(user!.MfaEnabled);
        Assert.NotNull(user.MfaSecret);
    }

    [Fact]
    public async Task MfaSetup_CanBeCalledMultipleTimes_BeforeVerification()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "mfarepeat@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "mfarepeat@example.com", "SecurePass1"));
        Assert.NotNull(loginResult.Value);
        Assert.NotNull(loginResult.Value.User);
        var userId = loginResult.Value.User.Id;

        var first = await _mfa.SetupMfaAsync(userId);
        Assert.True(first.IsSuccess);

        var second = await _mfa.SetupMfaAsync(userId);
        Assert.True(second.IsSuccess); // re-setup overwrites secret

        // MFA should NOT be enabled (setup alone doesn't enable)
        var user = await _repo.GetByPublicIdAsync(userId);
        Assert.NotNull(user);
        Assert.False(user!.MfaEnabled);
    }

    [Fact]
    public async Task MfaSetup_SecretIsEncryptedAtRest_NotPlaintext()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "mfaencrypt@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "mfaencrypt@example.com", "SecurePass1"));
        Assert.NotNull(loginResult.Value);
        Assert.NotNull(loginResult.Value.User);
        var userId = loginResult.Value.User.Id;

        var result = await _mfa.SetupMfaAsync(userId);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var user = await _repo.GetByPublicIdAsync(userId);
        Assert.NotNull(user);
        Assert.NotNull(user!.MfaSecret);

        // The plaintext secret (Base32) must differ from the stored MfaSecret (Base64 of protected bytes).
        // This proves SetupMfaAsync transforms the key before storage via IDataProtector.
        Assert.NotEqual(result.Value.Secret, user.MfaSecret);
        Assert.DoesNotContain(result.Value.Secret, user.MfaSecret, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MfaVerify_WithValidCode_EnablesMfaAndReturnsBackupCodes()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "mfaverify@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "mfaverify@example.com", "SecurePass1"));
        Assert.NotNull(loginResult.Value);
        Assert.NotNull(loginResult.Value.User);
        var userId = loginResult.Value.User.Id;

        var setupResult = await _mfa.SetupMfaAsync(userId);
        Assert.True(setupResult.IsSuccess);

        // Generate a valid TOTP code from the secret
        var secretBytes = OtpNet.Base32Encoding.ToBytes(setupResult.Value!.Secret);
        var totp = new OtpNet.Totp(secretBytes, step: 30);
        var validCode = totp.ComputeTotp();

        var result = await _mfa.VerifyMfaAsync(userId, validCode);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.BackupCodes);
        Assert.Equal(8, result.Value.BackupCodes.Length);

        // MFA should now be enabled
        var user = await _repo.GetByPublicIdAsync(userId);
        Assert.NotNull(user);
        Assert.True(user!.MfaEnabled);
    }

    [Fact]
    public async Task MfaVerify_WithInvalidCode_ReturnsError()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "mfabadcode@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "mfabadcode@example.com", "SecurePass1"));
        Assert.NotNull(loginResult.Value);
        Assert.NotNull(loginResult.Value.User);
        var userId = loginResult.Value.User.Id;

        await _mfa.SetupMfaAsync(userId);

        var result = await _mfa.VerifyMfaAsync(userId, "000000");

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_CODE", result.Error?.Code);
    }

    [Fact]
    public async Task MfaVerify_WithoutSetup_ReturnsError()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "mfanosetup@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "mfanosetup@example.com", "SecurePass1"));
        Assert.NotNull(loginResult.Value);
        Assert.NotNull(loginResult.Value.User);
        var userId = loginResult.Value.User.Id;

        var result = await _mfa.VerifyMfaAsync(userId, "123456");

        Assert.True(result.IsFailure);
        Assert.Equal("MFA_NOT_SETUP", result.Error?.Code);
    }

    [Fact]
    public async Task MfaVerify_WithMfaAlreadyEnabled_ReturnsError()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "mfaalready@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var loginResult = await _sut.LoginAsync(new LoginRequest(
            "mfaalready@example.com", "SecurePass1"));
        Assert.NotNull(loginResult.Value);
        Assert.NotNull(loginResult.Value.User);
        var userId = loginResult.Value.User.Id;

        var setup = await _mfa.SetupMfaAsync(userId);
        Assert.True(setup.IsSuccess);

        // Verify with valid code to enable MFA
        var secretBytes = OtpNet.Base32Encoding.ToBytes(setup.Value!.Secret);
        var totp = new OtpNet.Totp(secretBytes, step: 30);
        var validCode = totp.ComputeTotp();

        var firstVerify = await _mfa.VerifyMfaAsync(userId, validCode);
        Assert.True(firstVerify.IsSuccess);

        // Second verify should fail (MFA already enabled)
        var secondVerify = await _mfa.VerifyMfaAsync(userId, validCode);
        Assert.True(secondVerify.IsFailure);
        Assert.Equal("MFA_ALREADY_ENABLED", secondVerify.Error?.Code);
    }

    // --- MFA Login + Authenticate ---

    [Fact]
    public async Task Login_WithMfaEnabled_ReturnsMfaRequired()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "mfalogin@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        // Login first to get userId
        var firstLogin = await _sut.LoginAsync(new LoginRequest(
            "mfalogin@example.com", "SecurePass1"));
        var userId = firstLogin.Value!.User!.Id;

        // Setup + enable MFA
        var setup = await _mfa.SetupMfaAsync(userId);
        var secretBytes = OtpNet.Base32Encoding.ToBytes(setup.Value!.Secret);
        var totp = new OtpNet.Totp(secretBytes, step: 30);
        await _mfa.VerifyMfaAsync(userId, totp.ComputeTotp());

        // Login again — should return MfaRequired
        var secondLogin = await _sut.LoginAsync(new LoginRequest(
            "mfalogin@example.com", "SecurePass1"));

        Assert.True(secondLogin.IsSuccess);
        Assert.NotNull(secondLogin.Value);
        Assert.True(secondLogin.Value.MfaRequired);
        Assert.NotNull(secondLogin.Value.MfaToken);
        Assert.Null(secondLogin.Value.AccessToken);
        Assert.Null(secondLogin.Value.RefreshToken);
    }

    [Fact]
    public async Task AuthenticateWithMfa_WithValidTokenAndCode_ReturnsTokens()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "mfaauth@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var firstLogin = await _sut.LoginAsync(new LoginRequest(
            "mfaauth@example.com", "SecurePass1"));
        var userId = firstLogin.Value!.User!.Id;

        // Setup + enable MFA
        var setup = await _mfa.SetupMfaAsync(userId);
        var secretBytes = OtpNet.Base32Encoding.ToBytes(setup.Value!.Secret);
        var totp = new OtpNet.Totp(secretBytes, step: 30);
        await _mfa.VerifyMfaAsync(userId, totp.ComputeTotp());

        // Login to get MFA token
        var mfaLogin = await _sut.LoginAsync(new LoginRequest(
            "mfaauth@example.com", "SecurePass1"));
        var mfaToken = mfaLogin.Value!.MfaToken;

        // Compute fresh TOTP code
        var validCode = totp.ComputeTotp();

        // Authenticate with MFA
        var result = await _mfa.AuthenticateWithMfaAsync(mfaToken!, validCode);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.AccessToken);
        Assert.NotNull(result.Value.RefreshToken);
    }

    [Fact]
    public async Task AuthenticateWithMfa_WithInvalidCode_ReturnsError()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "mfabadcode2@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var firstLogin = await _sut.LoginAsync(new LoginRequest(
            "mfabadcode2@example.com", "SecurePass1"));
        var userId = firstLogin.Value!.User!.Id;

        var setup = await _mfa.SetupMfaAsync(userId);
        var secretBytes = OtpNet.Base32Encoding.ToBytes(setup.Value!.Secret);
        var totp = new OtpNet.Totp(secretBytes, step: 30);
        await _mfa.VerifyMfaAsync(userId, totp.ComputeTotp());

        var mfaLogin = await _sut.LoginAsync(new LoginRequest(
            "mfabadcode2@example.com", "SecurePass1"));
        var mfaToken = mfaLogin.Value!.MfaToken;

        var result = await _mfa.AuthenticateWithMfaAsync(mfaToken!, "000000");

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_CODE", result.Error?.Code);
    }

    [Fact]
    public async Task AuthenticateWithMfa_WithInvalidToken_ReturnsError()
    {
        var result = await _mfa.AuthenticateWithMfaAsync("invalid-token", "123456");

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_MFA_TOKEN", result.Error?.Code);
    }

    // --- MFA Backup Code Authenticate ---

    [Fact]
    public async Task AuthenticateWithMfa_WithValidBackupCode_ReturnsTokens()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "bcvalid@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var firstLogin = await _sut.LoginAsync(new LoginRequest(
            "bcvalid@example.com", "SecurePass1"));
        var userId = firstLogin.Value!.User!.Id;

        // Setup + enable MFA
        var setup = await _mfa.SetupMfaAsync(userId);
        var secretBytes = OtpNet.Base32Encoding.ToBytes(setup.Value!.Secret);
        var totp = new OtpNet.Totp(secretBytes, step: 30);
        var verifyResult = await _mfa.VerifyMfaAsync(userId, totp.ComputeTotp());
        Assert.True(verifyResult.IsSuccess);
        var backupCodes = verifyResult.Value!.BackupCodes;

        // Login to get MFA token
        var mfaLogin = await _sut.LoginAsync(new LoginRequest(
            "bcvalid@example.com", "SecurePass1"));
        var mfaToken = mfaLogin.Value!.MfaToken;

        // Authenticate with backup code
        var result = await _mfa.AuthenticateWithMfaAsync(mfaToken!, backupCodes[0]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.AccessToken);
        Assert.NotNull(result.Value.RefreshToken);
    }

    [Fact]
    public async Task AuthenticateWithMfa_WithUsedBackupCode_ReturnsError()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "bcused@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var firstLogin = await _sut.LoginAsync(new LoginRequest(
            "bcused@example.com", "SecurePass1"));
        var userId = firstLogin.Value!.User!.Id;

        // Setup + enable MFA
        var setup = await _mfa.SetupMfaAsync(userId);
        var secretBytes = OtpNet.Base32Encoding.ToBytes(setup.Value!.Secret);
        var totp = new OtpNet.Totp(secretBytes, step: 30);
        var verifyResult = await _mfa.VerifyMfaAsync(userId, totp.ComputeTotp());
        Assert.True(verifyResult.IsSuccess);
        var backupCodes = verifyResult.Value!.BackupCodes;

        // Login and authenticate once with backup code
        var login1 = await _sut.LoginAsync(new LoginRequest(
            "bcused@example.com", "SecurePass1"));
        var token1 = login1.Value!.MfaToken;
        await _mfa.AuthenticateWithMfaAsync(token1!, backupCodes[0]);

        // Login again and try the same backup code — should fail
        var login2 = await _sut.LoginAsync(new LoginRequest(
            "bcused@example.com", "SecurePass1"));
        var token2 = login2.Value!.MfaToken;
        var result = await _mfa.AuthenticateWithMfaAsync(token2!, backupCodes[0]);

        Assert.True(result.IsFailure);
        // Service falls through TOTP path and returns TOTP's INVALID_CODE
        // when neither TOTP nor backup code succeed.
        Assert.Equal("INVALID_CODE", result.Error?.Code);
    }

    [Fact]
    public async Task AuthenticateWithMfa_WithInvalidBackupCode_ReturnsError()
    {
        await _sut.RegisterAsync(new RegisterRequest(
            "bcinvalid@example.com", "SecurePass1", "User"));
        var verToken = _email.SentMessages[0].Token;
        await _sut.VerifyEmailAsync(new VerifyEmailRequest(verToken));

        var firstLogin = await _sut.LoginAsync(new LoginRequest(
            "bcinvalid@example.com", "SecurePass1"));
        var userId = firstLogin.Value!.User!.Id;

        // Setup + enable MFA
        var setup = await _mfa.SetupMfaAsync(userId);
        var secretBytes = OtpNet.Base32Encoding.ToBytes(setup.Value!.Secret);
        var totp = new OtpNet.Totp(secretBytes, step: 30);
        await _mfa.VerifyMfaAsync(userId, totp.ComputeTotp());

        var mfaLogin = await _sut.LoginAsync(new LoginRequest(
            "bcinvalid@example.com", "SecurePass1"));
        var mfaToken = mfaLogin.Value!.MfaToken;

        // Use a code that is neither a valid TOTP nor a valid backup code
        var result = await _mfa.AuthenticateWithMfaAsync(mfaToken!, "XXXXXXXX");

        Assert.True(result.IsFailure);
        // Service falls through TOTP path and returns TOTP's INVALID_CODE
        // when neither TOTP nor backup code succeed.
        Assert.Equal("INVALID_CODE", result.Error?.Code);
    }
}

// --- Fakes ---

sealed class FakeUserRepository : IUserRepository
{
    private long _nextId = 1;
    private readonly List<User> _users = [];
    private readonly List<RefreshToken> _refreshTokens = [];
    private readonly List<EmailVerificationToken> _verificationTokens = [];
    private readonly List<PasswordResetToken> _passwordResetTokens = [];
    private readonly List<MfaBackupCode> _backupCodes = [];

    public void Clear()
    {
        _users.Clear();
        _refreshTokens.Clear();
        _verificationTokens.Clear();
        _passwordResetTokens.Clear();
        _backupCodes.Clear();
        _nextId = 1;
    }

    // Users
    public Task<User?> GetByIdAsync(long id) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email) =>
        Task.FromResult(_users.FirstOrDefault(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByPublicIdAsync(Guid publicId) =>
        Task.FromResult(_users.FirstOrDefault(u => u.PublicId == publicId));

    public Task<long> CreateAsync(User user)
    {
        var id = _nextId++;
        user.Id = id;
        _users.Add(user);
        return Task.FromResult(id);
    }

    public Task UpdateAsync(User user)
    {
        var idx = _users.FindIndex(u => u.Id == user.Id);
        if (idx >= 0) _users[idx] = user;
        return Task.CompletedTask;
    }

    // Refresh tokens
    public Task CreateRefreshTokenAsync(RefreshToken token)
    {
        token.Id = _nextId++;
        _refreshTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash) =>
        Task.FromResult(_refreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task MarkRefreshTokenUsedAsync(long tokenId)
    {
        var idx = _refreshTokens.FindIndex(t => t.Id == tokenId);
        if (idx >= 0) _refreshTokens[idx].IsUsed = true;
        return Task.CompletedTask;
    }

    public Task RevokeRefreshTokenFamilyAsync(Guid family, long userId)
    {
        for (var i = 0; i < _refreshTokens.Count; i++)
        {
            if (_refreshTokens[i].Family == family && _refreshTokens[i].UserId == userId)
                _refreshTokens[i].IsUsed = true;
        }
        return Task.CompletedTask;
    }

    public Task RevokeUserRefreshTokensAsync(long userId)
    {
        for (var i = 0; i < _refreshTokens.Count; i++)
        {
            if (_refreshTokens[i].UserId == userId && !_refreshTokens[i].IsUsed)
                _refreshTokens[i].IsUsed = true;
        }
        return Task.CompletedTask;
    }

    // Verification tokens
    public Task CreateVerificationTokenAsync(EmailVerificationToken token)
    {
        token.Id = _nextId++;
        _verificationTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<EmailVerificationToken?> GetVerificationTokenByHashAsync(string tokenHash) =>
        Task.FromResult(_verificationTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task MarkVerificationTokenUsedAsync(long tokenId)
    {
        var idx = _verificationTokens.FindIndex(t => t.Id == tokenId);
        if (idx >= 0)
            _verificationTokens[idx].UsedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    // Password reset tokens
    public Task CreatePasswordResetTokenAsync(PasswordResetToken token)
    {
        token.Id = _nextId++;
        _passwordResetTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash) =>
        Task.FromResult(_passwordResetTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task MarkPasswordResetTokenUsedAsync(long tokenId)
    {
        var idx = _passwordResetTokens.FindIndex(t => t.Id == tokenId);
        if (idx >= 0)
            _passwordResetTokens[idx].UsedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    // MFA backup codes
    public Task CreateBackupCodeAsync(MfaBackupCode code)
    {
        code.Id = _nextId++;
        _backupCodes.Add(code);
        return Task.CompletedTask;
    }

    public Task<List<MfaBackupCode>> GetBackupCodesByUserIdAsync(long userId) =>
        Task.FromResult(_backupCodes.Where(c => c.UserId == userId).ToList());

    public Task MarkBackupCodeUsedAsync(long codeId)
    {
        var idx = _backupCodes.FindIndex(c => c.Id == codeId);
        if (idx >= 0)
            _backupCodes[idx].IsUsed = true;
        return Task.CompletedTask;
    }
}

sealed class FakeDataProtectionProvider : IDataProtectionProvider
{
    public IDataProtector CreateProtector(string purpose) => new FakeDataProtector();
}

sealed class FakeDataProtector : IDataProtector
{
    public IDataProtector CreateProtector(string purpose) => this;
    public byte[] Protect(byte[] plaintext) => plaintext;
    public byte[] Unprotect(byte[] protectedData) => protectedData;
}

sealed class FakeEmailSender : IEmailSender
{
    public List<(string Email, string Token)> SentMessages { get; } = [];

    public Task SendVerificationEmailAsync(string email, string token)
    {
        SentMessages.Add((email, token));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string email, string token)
    {
        SentMessages.Add((email, token));
        return Task.CompletedTask;
    }
}
