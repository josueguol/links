namespace Links.Api.Modules.Posts;

public sealed class Post
{
    public long Id { get; set; }
    public Guid PublicId { get; init; }
    public long AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
