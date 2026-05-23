namespace Links.Api.Modules.Auth;

public interface IEmailSender
{
    Task SendVerificationEmailAsync(string email, string token);
    Task SendPasswordResetEmailAsync(string email, string token);
}
