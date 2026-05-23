using Dapper;
using Npgsql;

namespace Links.Api.Modules.Posts;

public sealed class PostRepository : IPostRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<Post?> GetByIdAsync(long id)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Post>(
            "SELECT * FROM posts WHERE id = @Id", new { Id = id });
    }

    public async Task<Post?> GetByPublicIdAsync(Guid publicId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Post>(
            "SELECT * FROM posts WHERE public_id = @PublicId",
            new { PublicId = publicId });
    }

    public async Task<long> CreateAsync(Post post)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO posts (public_id, author_id, content, created_at, updated_at)
            VALUES (@PublicId, @AuthorId, @Content, @CreatedAt, @UpdatedAt)
            RETURNING id
            """;
        return await conn.ExecuteScalarAsync<long>(sql, post);
    }

    public async Task<IReadOnlyList<PostFeedRow>> GetFeedAsync(
        DateTimeOffset? cursorCreatedAt, long? cursorId, int limit)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Fetch one extra row to determine if there is a next page.
        var fetchLimit = limit + 1;

        var sql = """
            SELECT p.id, p.public_id, p.author_id, p.content,
                   p.created_at, p.updated_at,
                   u.public_id AS author_public_id, u.display_name AS author_display_name
            FROM posts p
            INNER JOIN users u ON u.id = p.author_id
            WHERE (@CursorCreatedAt IS NULL OR
                   p.created_at < @CursorCreatedAt OR
                   (p.created_at = @CursorCreatedAt AND p.id < @CursorId))
            ORDER BY p.created_at DESC, p.id DESC
            LIMIT @Limit
            """;

        var rows = await conn.QueryAsync<PostFeedRow>(sql, new
        {
            CursorCreatedAt = cursorCreatedAt,
            CursorId = cursorId,
            Limit = fetchLimit
        });

        return rows.AsList();
    }
}
