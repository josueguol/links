using Links.Api.Common;
using Links.Api.Modules.Auth;
using Links.Api.Modules.Posts;
using Links.Tests.Modules.Auth;

namespace Links.Tests.Modules.Posts;

public sealed class PostServiceTests : IDisposable
{
    private readonly PostService _sut;
    private readonly FakePostRepository _repo;
    private readonly FakeUserRepository _userRepo;

    public PostServiceTests()
    {
        _userRepo = new FakeUserRepository();
        _repo = new FakePostRepository(_userRepo);
        _sut = new PostService(_repo, _userRepo);
    }

    public void Dispose()
    {
        _repo.Clear();
        _userRepo.Clear();
    }

    // --- CreatePost ---

    [Fact]
    public async Task CreatePost_WithValidData_ReturnsPostResponse()
    {
        var (_, authorPublicId) = await CreateUserAsync("author@example.com", "Author");

        var result = await _sut.CreatePostAsync(
            new CreatePostRequest("Hello **world**!"), authorPublicId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Hello **world**!", result.Value.Content);
        Assert.Equal(authorPublicId, result.Value.Author.Id);
        Assert.Equal("Author", result.Value.Author.DisplayName);
        Assert.NotEqual(default, result.Value.CreatedAt);
    }

    [Fact]
    public async Task CreatePost_WithNonExistentAuthor_ReturnsError()
    {
        var result = await _sut.CreatePostAsync(
            new CreatePostRequest("Content"), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("USER_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task CreatePost_TrimsContent()
    {
        var (_, authorPublicId) = await CreateUserAsync("trim@example.com", "Trimmer");

        var result = await _sut.CreatePostAsync(
            new CreatePostRequest("  Spaced content  "), authorPublicId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Spaced content", result.Value.Content);
    }

    // --- GetPost ---

    [Fact]
    public async Task GetPost_WithExistingId_ReturnsPostResponse()
    {
        var (_, authorPublicId) = await CreateUserAsync("getpost@example.com", "Getter");
        var createResult = await _sut.CreatePostAsync(
            new CreatePostRequest("Find me"), authorPublicId);

        Assert.NotNull(createResult.Value);
        var postId = createResult.Value.Id;

        var result = await _sut.GetPostAsync(postId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Find me", result.Value.Content);
        Assert.Equal(authorPublicId, result.Value.Author.Id);
    }

    [Fact]
    public async Task GetPost_WithNonExistentId_ReturnsError()
    {
        var result = await _sut.GetPostAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("POST_NOT_FOUND", result.Error?.Code);
    }

    // --- GetFeed ---

    [Fact]
    public async Task GetFeed_ReturnsPostsInReverseChronologicalOrder()
    {
        var (_, authorPublicId) = await CreateUserAsync("feed@example.com", "Feeder");

        await _sut.CreatePostAsync(new CreatePostRequest("First"), authorPublicId);
        await _sut.CreatePostAsync(new CreatePostRequest("Second"), authorPublicId);
        await _sut.CreatePostAsync(new CreatePostRequest("Third"), authorPublicId);

        var feed = await _sut.GetFeedAsync(null, 10);

        Assert.Equal(3, feed.Posts.Count);
        Assert.Equal("Third", feed.Posts[0].Content);
        Assert.Equal("Second", feed.Posts[1].Content);
        Assert.Equal("First", feed.Posts[2].Content);
    }

    [Fact]
    public async Task GetFeed_WithNoPosts_ReturnsEmptyList()
    {
        var feed = await _sut.GetFeedAsync(null, 10);

        Assert.Empty(feed.Posts);
        Assert.Null(feed.NextCursor);
    }

    [Fact]
    public async Task GetFeed_ReturnsNullCursor_OnLastPage()
    {
        var (_, authorPublicId) = await CreateUserAsync("lastpage@example.com", "Last");

        await _sut.CreatePostAsync(new CreatePostRequest("Only post"), authorPublicId);

        var feed = await _sut.GetFeedAsync(null, 10);

        Assert.Single(feed.Posts);
        Assert.Null(feed.NextCursor);
    }

    [Fact]
    public async Task GetFeed_ReturnsNextCursor_WhenMorePagesExist()
    {
        var (_, authorPublicId) = await CreateUserAsync("pages@example.com", "Pager");

        for (var i = 0; i < 3; i++)
        {
            await _sut.CreatePostAsync(new CreatePostRequest($"Post {i}"), authorPublicId);
        }

        var feed = await _sut.GetFeedAsync(null, 2);

        Assert.Equal(2, feed.Posts.Count);
        Assert.NotNull(feed.NextCursor);
    }

    [Fact]
    public async Task GetFeed_PaginatesWithCursor_Correctly()
    {
        var (_, authorPublicId) = await CreateUserAsync("cursor@example.com", "Cursor");

        for (var i = 0; i < 4; i++)
        {
            await _sut.CreatePostAsync(new CreatePostRequest($"Post {i}"), authorPublicId);
        }

        // First page: limit 2
        var page1 = await _sut.GetFeedAsync(null, 2);
        Assert.Equal(2, page1.Posts.Count);
        Assert.NotNull(page1.NextCursor);
        Assert.Equal("Post 3", page1.Posts[0].Content);
        Assert.Equal("Post 2", page1.Posts[1].Content);

        // Second page — should contain the remaining posts
        var page2 = await _sut.GetFeedAsync(page1.NextCursor, 2);
        Assert.Equal(2, page2.Posts.Count);
        Assert.Null(page2.NextCursor);
        Assert.Equal("Post 1", page2.Posts[0].Content);
        Assert.Equal("Post 0", page2.Posts[1].Content);
    }

    [Fact]
    public async Task GetFeed_ClampsLimitToMax50()
    {
        var (_, authorPublicId) = await CreateUserAsync("maxlimit@example.com", "Max");

        for (var i = 0; i < 60; i++)
        {
            await _sut.CreatePostAsync(new CreatePostRequest($"Post {i}"), authorPublicId);
        }

        var feed = await _sut.GetFeedAsync(null, 100);

        Assert.Equal(50, feed.Posts.Count);
        Assert.NotNull(feed.NextCursor);
    }

    [Fact]
    public async Task GetFeed_ClampsLimitToMinimum1()
    {
        var (_, authorPublicId) = await CreateUserAsync("minlimit@example.com", "Min");

        await _sut.CreatePostAsync(new CreatePostRequest("A"), authorPublicId);
        await _sut.CreatePostAsync(new CreatePostRequest("B"), authorPublicId);

        var feed = await _sut.GetFeedAsync(null, 0);

        Assert.Single(feed.Posts);
    }

    // --- Helpers ---

    private async Task<(long UserId, Guid PublicId)> CreateUserAsync(
        string email, string displayName)
    {
        var publicId = Guid.NewGuid();
        var user = new User
        {
            PublicId = publicId,
            Email = email,
            PasswordHash = "hash",
            DisplayName = displayName,
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var id = await _userRepo.CreateAsync(user);
        return (id, publicId);
    }
}

// --- Fakes ---

sealed class FakePostRepository : IPostRepository
{
    private long _nextId = 1;
    private readonly List<Post> _posts = [];
    private readonly FakeUserRepository _userRepo;

    public FakePostRepository(FakeUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public void Clear()
    {
        _posts.Clear();
        _nextId = 1;
    }

    public Task<Post?> GetByIdAsync(long id) =>
        Task.FromResult(_posts.FirstOrDefault(p => p.Id == id));

    public Task<Post?> GetByPublicIdAsync(Guid publicId) =>
        Task.FromResult(_posts.FirstOrDefault(p => p.PublicId == publicId));

    public Task<long> CreateAsync(Post post)
    {
        var id = _nextId++;
        post.Id = id;
        _posts.Add(post);
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<PostFeedRow>> GetFeedAsync(
        DateTimeOffset? cursorCreatedAt, long? cursorId, int limit)
    {
        var query = _posts.AsEnumerable();

        if (cursorCreatedAt.HasValue)
        {
            query = query.Where(p =>
                p.CreatedAt < cursorCreatedAt.Value ||
                (p.CreatedAt == cursorCreatedAt.Value && p.Id < cursorId));
        }

        // Fetch one extra to detect next page (matches PostRepository behavior).
        var fetchLimit = limit + 1;

        var rows = query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(fetchLimit)
            .Select(p =>
            {
                var user = _userRepo.GetByIdAsync(p.AuthorId).GetAwaiter().GetResult();
                return new PostFeedRow(
                    p.Id,
                    p.PublicId,
                    p.AuthorId,
                    p.Content,
                    p.CreatedAt,
                    p.UpdatedAt,
                    user?.PublicId ?? Guid.Empty,
                    user?.DisplayName ?? "Unknown");
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<PostFeedRow>>(rows);
    }
}
