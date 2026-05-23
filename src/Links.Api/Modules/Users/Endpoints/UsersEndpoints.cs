using Links.Api.Common;

namespace Links.Api.Modules.Users.Endpoints;

public static class UsersEndpoints
{
    public static RouteGroupBuilder MapUsersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization();

        group.MapGet("/me", async (
            HttpContext context,
            UserService userService) =>
        {
            var userId = UserService.GetCurrentUserId(context.User);
            var result = await userService.GetMyProfileAsync(userId);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error!.Value);
        });

        return group;
    }

    private static IResult MapError(Error error)
    {
        return error.Code switch
        {
            "USER_NOT_FOUND" => Results.NotFound(new { error.Code, error.Message }),
            _ => Results.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest)
        };
    }
}
