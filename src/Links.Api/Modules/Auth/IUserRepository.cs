namespace Links.Api.Modules.Auth;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(long id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByPublicIdAsync(Guid publicId);
    Task<long> CreateAsync(User user);
    Task UpdateAsync(User user);

    Task CreateRefreshTokenAsync(RefreshToken token);
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash);
    Task MarkRefreshTokenUsedAsync(long tokenId);
    Task RevokeRefreshTokenFamilyAsync(Guid family, long userId);
    Task RevokeUserRefreshTokensAsync(long userId);

    Task CreateVerificationTokenAsync(EmailVerificationToken token);
    Task<EmailVerificationToken?> GetVerificationTokenByHashAsync(string tokenHash);
    Task MarkVerificationTokenUsedAsync(long tokenId);

    Task CreatePasswordResetTokenAsync(PasswordResetToken token);
    Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash);
    Task MarkPasswordResetTokenUsedAsync(long tokenId);

    Task CreateBackupCodeAsync(MfaBackupCode code);
    Task<List<MfaBackupCode>> GetBackupCodesByUserIdAsync(long userId);
    Task MarkBackupCodeUsedAsync(long codeId);
}
