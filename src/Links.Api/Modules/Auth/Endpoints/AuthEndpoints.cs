using Links.Api.Common;

namespace Links.Api.Modules.Auth.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            AuthService authService) =>
        {
            var result = await authService.RegisterAsync(request);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error!.Value);
        }).AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        group.MapPost("/verify-email", async (
            VerifyEmailRequest request,
            AuthService authService) =>
        {
            var result = await authService.VerifyEmailAsync(request);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error!.Value);
        }).AddEndpointFilter<ValidationFilter<VerifyEmailRequest>>();

        group.MapPost("/login", async (
            LoginRequest request,
            AuthService authService,
            HttpContext context) =>
        {
            var result = await authService.LoginAsync(request);
            if (result.IsFailure)
                return MapError(result.Error!.Value);

            if (result.Value!.MfaRequired)
            {
                return Results.Ok(new
                {
                    mfa_required = true,
                    mfa_token = result.Value.MfaToken
                });
            }

            SetRefreshCookie(context, result.Value.RefreshToken!);
            return Results.Ok(new
            {
                result.Value.AccessToken,
                User = result.Value.User
            });
        }).AddEndpointFilter<ValidationFilter<LoginRequest>>();

        group.MapPost("/refresh", async (
            HttpContext context,
            AuthService authService) =>
        {
            var refreshToken = context.Request.Cookies["links_refresh_token"];
            if (string.IsNullOrEmpty(refreshToken))
                return Results.Unauthorized();

            var result = await authService.RefreshTokenAsync(refreshToken);
            if (result.IsFailure)
                return MapError(result.Error!.Value);

            SetRefreshCookie(context, result.Value!.RefreshToken);
            return Results.Ok(new
            {
                result.Value.AccessToken,
                User = result.Value.User
            });
        });

        group.MapPost("/logout", async (
            HttpContext context,
            AuthService authService) =>
        {
            var refreshToken = context.Request.Cookies["links_refresh_token"];
            if (!string.IsNullOrEmpty(refreshToken))
                await authService.LogoutAsync(refreshToken);

            context.Response.Cookies.Delete("links_refresh_token", new CookieOptions
            {
                Path = "/api/auth"
            });

            return Results.Ok(new { message = "Logged out." });
        });

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest request,
            AuthService authService) =>
        {
            var result = await authService.ForgotPasswordAsync(request);
            // Always return OK to avoid revealing email existence
            return Results.Ok(result.Value);
        }).AddEndpointFilter<ValidationFilter<ForgotPasswordRequest>>();

        group.MapPost("/reset-password", async (
            ResetPasswordRequest request,
            AuthService authService) =>
        {
            var result = await authService.ResetPasswordAsync(request);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error!.Value);
        }).AddEndpointFilter<ValidationFilter<ResetPasswordRequest>>();

        // --- MFA ---

        group.MapPost("/mfa/setup", async (
            HttpContext context,
            MfaService mfaService) =>
        {
            var userId = context.User.GetUserId();
            if (userId is null)
                return Results.Unauthorized();

            var result = await mfaService.SetupMfaAsync(userId.Value);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapMfaError(result.Error!.Value);
        }).RequireAuthorization();

        group.MapPost("/mfa/verify", async (
            MfaVerifyRequest request,
            HttpContext context,
            MfaService mfaService) =>
        {
            var userId = context.User.GetUserId();
            if (userId is null)
                return Results.Unauthorized();

            var result = await mfaService.VerifyMfaAsync(userId.Value, request.Code);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapMfaError(result.Error!.Value);
        }).RequireAuthorization()
          .AddEndpointFilter<ValidationFilter<MfaVerifyRequest>>();

        group.MapPost("/mfa/authenticate", async (
            MfaAuthenticateRequest request,
            MfaService mfaService,
            HttpContext context) =>
        {
            var result = await mfaService.AuthenticateWithMfaAsync(
                request.MfaToken, request.Code);

            if (result.IsFailure)
                return MapMfaError(result.Error!.Value);

            SetRefreshCookie(context, result.Value!.RefreshToken);
            return Results.Ok(new
            {
                result.Value.AccessToken,
                User = result.Value.User
            });
        }).AddEndpointFilter<ValidationFilter<MfaAuthenticateRequest>>();

        return group;
    }

    private static void SetRefreshCookie(HttpContext context, string token)
    {
        context.Response.Cookies.Append("links_refresh_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            MaxAge = TimeSpan.FromDays(30)
        });
    }

    private static IResult MapError(Error error)
    {
        return error.Code switch
        {
            "EMAIL_EXISTS" => Results.Conflict(new { error.Code, error.Message }),
            "INVALID_CREDENTIALS" => Results.Unauthorized(),
            "EMAIL_NOT_VERIFIED" => Results.Json(
                new { error.Code, error.Message }, statusCode: StatusCodes.Status403Forbidden),
            "TOKEN_REUSED" => Results.Json(
                new { error.Code, error.Message }, statusCode: StatusCodes.Status401Unauthorized),
            "TOKEN_EXPIRED" => Results.Json(
                new { error.Code, error.Message }, statusCode: StatusCodes.Status401Unauthorized),
            "INVALID_TOKEN" => Results.Json(
                new { error.Code, error.Message }, statusCode: StatusCodes.Status401Unauthorized),
            "TOKEN_USED" => Results.Conflict(new { error.Code, error.Message }),
            "USER_NOT_FOUND" => Results.NotFound(new { error.Code, error.Message }),
            _ => Results.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest)
        };
    }

    private static IResult MapMfaError(Error error)
    {
        return error.Code switch
        {
            "MFA_ALREADY_ENABLED" => Results.Conflict(new { error.Code, error.Message }),
            "MFA_NOT_SETUP" => Results.BadRequest(new { error.Code, error.Message }),
            "MFA_NOT_ENABLED" => Results.BadRequest(new { error.Code, error.Message }),
            "INVALID_CODE" => Results.Json(
                new { error.Code, error.Message }, statusCode: StatusCodes.Status401Unauthorized),
            "INVALID_BACKUP_CODE" => Results.Json(
                new { error.Code, error.Message }, statusCode: StatusCodes.Status401Unauthorized),
            "INVALID_MFA_TOKEN" => Results.Json(
                new { error.Code, error.Message }, statusCode: StatusCodes.Status401Unauthorized),
            _ => Results.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest)
        };
    }
}
