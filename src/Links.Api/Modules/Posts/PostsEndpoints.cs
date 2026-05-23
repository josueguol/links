using Links.Api.Common;

namespace Links.Api.Modules.Posts;

public static class PostsEndpoints
{
    public static RouteGroupBuilder MapPostsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/posts").RequireAuthorization();

        group.MapPost("/", async (
            CreatePostRequest request,
            PostService postService,
            HttpContext context) =>
        {
            var userId = context.User.GetUserId();
            if (userId is null)
                return Results.Unauthorized();

            var result = await postService.CreatePostAsync(request, userId.Value);
            return result.IsSuccess
                ? Results.Created($"/api/posts/{result.Value!.Id}", result.Value)
                : MapError(result.Error!.Value);
        }).AddEndpointFilter<ValidationFilter<CreatePostRequest>>();

        group.MapGet("/", async (
            string? cursor,
            int? limit,
            PostService postService) =>
        {
            var response = await postService.GetFeedAsync(cursor, limit ?? 10);
            return Results.Ok(response);
        });

        group.MapGet("/{publicId:guid}", async (
            Guid publicId,
            PostService postService) =>
        {
            var result = await postService.GetPostAsync(publicId);
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
            "POST_NOT_FOUND" => Results.NotFound(new { error.Code, error.Message }),
            "USER_NOT_FOUND" => Results.NotFound(new { error.Code, error.Message }),
            _ => Results.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest)
        };
    }
}
