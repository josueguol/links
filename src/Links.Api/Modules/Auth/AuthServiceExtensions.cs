using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Links.Api.Modules.Auth;

public static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthModule(
        this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        // Data source
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
        services.AddSingleton(new NpgsqlDataSourceBuilder(connectionString).Build());

        // Auth services
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<TokenService>();
        services.AddScoped<IUserRepository, UserRepository>();

        if (isDevelopment)
        {
            services.AddScoped<IEmailSender, DevelopmentEmailSender>();
        }
        else
        {
            // Fail explicitly at startup — no real email provider is configured.
            // Register one before deploying to non-Development environments.
            throw new InvalidOperationException(
                "No real IEmailSender provider is configured. " +
                "Register a production-ready email sender service before deploying.");
        }

        services.AddScoped<AuthService>();
        services.AddScoped<MfaService>();

        // JWT authentication
        var jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

        // Data Protection — persist key ring to file system for Docker/production
        var dataProtectionKeyDir = configuration["DataProtection:KeyDirectory"] ?? "./keys";
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyDir))
            .SetApplicationName("Links");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "Links",
                    ValidateAudience = true,
                    ValidAudience = "Links.Web",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();

        return services;
    }
}
