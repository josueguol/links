namespace Links.Api.Modules.Posts;

// --- Requests ---

public sealed record CreatePostRequest(string Content);

// --- Responses ---

public sealed record PostResponse(
    Guid Id,
    AuthorResponse Author,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record AuthorResponse(Guid Id, string DisplayName);

public sealed record FeedResponse(
    IReadOnlyList<PostResponse> Posts,
    string? NextCursor
);
