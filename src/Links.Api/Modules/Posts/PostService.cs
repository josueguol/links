using System.Globalization;
using System.Text;
using Links.Api.Common;
using Links.Api.Modules.Auth;

namespace Links.Api.Modules.Posts;

public sealed class PostService
{
    private readonly IPostRepository _repo;
    private readonly IUserRepository _userRepo;

    public PostService(IPostRepository repo, IUserRepository userRepo)
    {
        _repo = repo;
        _userRepo = userRepo;
    }

    // --- Create ---

    public async Task<Result<PostResponse>> CreatePostAsync(
        CreatePostRequest request, Guid authorPublicId)
    {
        var user = await _userRepo.GetByPublicIdAsync(authorPublicId);
        if (user is null)
            return new Error("USER_NOT_FOUND", "Authenticated user not found.");

        var now = DateTimeOffset.UtcNow;
        var post = new Post
        {
            PublicId = Guid.NewGuid(),
            AuthorId = user.Id,
            Content = request.Content.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        var postId = await _repo.CreateAsync(post);
        var saved = await _repo.GetByIdAsync(postId);
        if (saved is null)
            return new Error("POST_CREATE_FAILED", "Failed to create post.");

        return new PostResponse(
            saved.PublicId,
            new AuthorResponse(user.PublicId, user.DisplayName),
            saved.Content,
            saved.CreatedAt,
            saved.UpdatedAt);
    }

    // --- Get by public ID ---

    public async Task<Result<PostResponse>> GetPostAsync(Guid publicId)
    {
        var post = await _repo.GetByPublicIdAsync(publicId);
        if (post is null)
            return new Error("POST_NOT_FOUND", "Post not found.");

        var user = await _userRepo.GetByIdAsync(post.AuthorId);
        if (user is null)
            return new Error("USER_NOT_FOUND", "Author not found.");

        return new PostResponse(
            post.PublicId,
            new AuthorResponse(user.PublicId, user.DisplayName),
            post.Content,
            post.CreatedAt,
            post.UpdatedAt);
    }

    // --- Feed ---

    public async Task<FeedResponse> GetFeedAsync(string? cursor, int limit = 10)
    {
        var clampedLimit = Math.Clamp(limit, 1, 50);

        DateTimeOffset? cursorCreatedAt = null;
        long? cursorId = null;

        if (!string.IsNullOrEmpty(cursor))
        {
            (cursorCreatedAt, cursorId) = DecodeCursor(cursor);
        }

        var rows = await _repo.GetFeedAsync(cursorCreatedAt, cursorId, clampedLimit);

        var posts = new List<PostResponse>(clampedLimit);
        string? nextCursor = null;

        for (var i = 0; i < rows.Count && i < clampedLimit; i++)
        {
            var row = rows[i];
            posts.Add(new PostResponse(
                row.PublicId,
                new AuthorResponse(row.AuthorPublicId, row.AuthorDisplayName),
                row.Content,
                row.CreatedAt,
                row.UpdatedAt));
        }

        // If we got more rows than the limit, there is a next page.
        if (rows.Count > clampedLimit)
        {
            var lastRow = rows[clampedLimit - 1];
            nextCursor = EncodeCursor(lastRow.CreatedAt, lastRow.Id);
        }

        return new FeedResponse(posts, nextCursor);
    }

    // --- Cursor helpers ---

    private static string EncodeCursor(DateTimeOffset createdAt, long id)
    {
        var raw = createdAt.ToString("O", CultureInfo.InvariantCulture)
                  + "|"
                  + id.ToString(CultureInfo.InvariantCulture);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    private static (DateTimeOffset CreatedAt, long Id) DecodeCursor(string cursor)
    {
        var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        var parts = raw.Split('|');
        var createdAt = DateTimeOffset.Parse(
            parts[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        var id = long.Parse(parts[1], CultureInfo.InvariantCulture);
        return (createdAt, id);
    }
}
