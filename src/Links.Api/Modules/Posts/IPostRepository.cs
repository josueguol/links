namespace Links.Api.Modules.Posts;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(long id);
    Task<Post?> GetByPublicIdAsync(Guid publicId);
    Task<long> CreateAsync(Post post);
    Task<IReadOnlyList<PostFeedRow>> GetFeedAsync(
        DateTimeOffset? cursorCreatedAt, long? cursorId, int limit);
}

/// <summary>
/// Internal DTO for the join between posts and users in the feed query.
/// </summary>
public sealed record PostFeedRow(
    long Id,
    Guid PublicId,
    long AuthorId,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid AuthorPublicId,
    string AuthorDisplayName
);
