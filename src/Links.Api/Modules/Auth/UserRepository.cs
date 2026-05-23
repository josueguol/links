using Dapper;
using Npgsql;

namespace Links.Api.Modules.Auth;

public sealed class UserRepository : IUserRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public UserRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    // --- Users ---

    public async Task<User?> GetByIdAsync(long id)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @Id", new { Id = id });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM users WHERE email = @Email", new { Email = email });
    }

    public async Task<User?> GetByPublicIdAsync(Guid publicId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM users WHERE public_id = @PublicId",
            new { PublicId = publicId });
    }

    public async Task<long> CreateAsync(User user)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO users (public_id, email, password_hash, display_name, created_at, updated_at)
            VALUES (@PublicId, @Email, @PasswordHash, @DisplayName, @CreatedAt, @UpdatedAt)
            RETURNING id
            """;
        return await conn.ExecuteScalarAsync<long>(sql, user);
    }

    public async Task UpdateAsync(User user)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        const string sql = """
            UPDATE users
            SET email = @Email, password_hash = @PasswordHash, display_name = @DisplayName,
                email_verified_at = @EmailVerifiedAt, mfa_enabled = @MfaEnabled,
                mfa_secret = @MfaSecret, updated_at = @UpdatedAt
            WHERE id = @Id
            """;
        await conn.ExecuteAsync(sql, user);
    }

    // --- Refresh tokens ---

    public async Task CreateRefreshTokenAsync(RefreshToken token)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO refresh_tokens (user_id, token_hash, family, expires_at, created_at)
            VALUES (@UserId, @TokenHash, @Family, @ExpiresAt, @CreatedAt)
            """;
        await conn.ExecuteAsync(sql, token);
    }

    public async Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<RefreshToken>(
            "SELECT * FROM refresh_tokens WHERE token_hash = @TokenHash",
            new { TokenHash = tokenHash });
    }

    public async Task MarkRefreshTokenUsedAsync(long tokenId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE refresh_tokens SET is_used = true WHERE id = @Id",
            new { Id = tokenId });
    }

    public async Task RevokeRefreshTokenFamilyAsync(Guid family, long userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE refresh_tokens SET is_used = true WHERE family = @Family AND user_id = @UserId",
            new { Family = family, UserId = userId });
    }

    public async Task RevokeUserRefreshTokensAsync(long userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE refresh_tokens SET is_used = true WHERE user_id = @UserId AND is_used = false",
            new { UserId = userId });
    }

    // --- Email verification tokens ---

    public async Task CreateVerificationTokenAsync(EmailVerificationToken token)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO email_verification_tokens (user_id, token_hash, expires_at, created_at)
            VALUES (@UserId, @TokenHash, @ExpiresAt, @CreatedAt)
            """;
        await conn.ExecuteAsync(sql, token);
    }

    public async Task<EmailVerificationToken?> GetVerificationTokenByHashAsync(string tokenHash)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<EmailVerificationToken>(
            "SELECT * FROM email_verification_tokens WHERE token_hash = @TokenHash",
            new { TokenHash = tokenHash });
    }

    public async Task MarkVerificationTokenUsedAsync(long tokenId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE email_verification_tokens SET used_at = @Now WHERE id = @Id",
            new { Id = tokenId, Now = DateTimeOffset.UtcNow });
    }

    // --- Password reset tokens ---

    public async Task CreatePasswordResetTokenAsync(PasswordResetToken token)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO password_reset_tokens (user_id, token_hash, expires_at, created_at)
            VALUES (@UserId, @TokenHash, @ExpiresAt, @CreatedAt)
            """;
        await conn.ExecuteAsync(sql, token);
    }

    public async Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<PasswordResetToken>(
            "SELECT * FROM password_reset_tokens WHERE token_hash = @TokenHash",
            new { TokenHash = tokenHash });
    }

    public async Task MarkPasswordResetTokenUsedAsync(long tokenId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE password_reset_tokens SET used_at = @Now WHERE id = @Id",
            new { Id = tokenId, Now = DateTimeOffset.UtcNow });
    }

    // --- MFA backup codes ---

    public async Task CreateBackupCodeAsync(MfaBackupCode code)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO mfa_backup_codes (user_id, code_hash, created_at)
            VALUES (@UserId, @CodeHash, @CreatedAt)
            """;
        await conn.ExecuteAsync(sql, code);
    }

    public async Task<List<MfaBackupCode>> GetBackupCodesByUserIdAsync(long userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        var rows = await conn.QueryAsync<MfaBackupCode>(
            "SELECT * FROM mfa_backup_codes WHERE user_id = @UserId ORDER BY id",
            new { UserId = userId });
        return rows.AsList();
    }

    public async Task MarkBackupCodeUsedAsync(long codeId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE mfa_backup_codes SET is_used = true WHERE id = @Id",
            new { Id = codeId });
    }
}
