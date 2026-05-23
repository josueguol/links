namespace Links.Api.Modules.Auth;

public sealed class MfaBackupCode
{
    public long Id { get; set; }
    public long UserId { get; init; }
    public string CodeHash { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}
