namespace Links.Api.Modules.Posts;

public static class PostsModuleExtensions
{
    public static IServiceCollection AddPostsModule(this IServiceCollection services)
    {
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<PostService>();
        return services;
    }
}
