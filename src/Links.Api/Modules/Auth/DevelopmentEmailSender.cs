namespace Links.Api.Modules.Auth;

public sealed class DevelopmentEmailSender : IEmailSender
{
    public Task SendVerificationEmailAsync(string email, string token)
    {
        Console.WriteLine($"[DEV EMAIL] To: {email}");
        Console.WriteLine($"[DEV EMAIL] Type: Email Verification");
        Console.WriteLine($"[DEV EMAIL] Token: {token}");
        Console.WriteLine($"[DEV EMAIL] Link: /api/auth/verify-email?token={token}");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string email, string token)
    {
        Console.WriteLine($"[DEV EMAIL] To: {email}");
        Console.WriteLine($"[DEV EMAIL] Type: Password Reset");
        Console.WriteLine($"[DEV EMAIL] Token: {token}");
        return Task.CompletedTask;
    }
}
