using Educatinal_Platform.Models;
using MongoDB.Driver;

namespace Educatinal_Platform.Repositories;

public interface ILessonCommentRepository
{
    Task<LessonComment> CreateAsync(LessonComment comment);
    Task<List<LessonComment>> GetByLessonIdAsync(string lessonId);
    Task EnsureIndexesAsync();
}

public sealed class LessonCommentRepository : ILessonCommentRepository
{
    private readonly IMongoCollection<LessonComment> _comments;

    public LessonCommentRepository(IMongoDatabase database)
    {
        _comments = database.GetCollection<LessonComment>("LessonComments");
    }

    public async Task<LessonComment> CreateAsync(LessonComment comment)
    {
        await _comments.InsertOneAsync(comment);
        return comment;
    }

    public Task<List<LessonComment>> GetByLessonIdAsync(string lessonId) => _comments
        .Find(comment => comment.LessonId == lessonId)
        .SortByDescending(comment => comment.CreatedAt)
        .ThenByDescending(comment => comment.Id)
        .ToListAsync();

    public Task EnsureIndexesAsync()
    {
        var keys = Builders<LessonComment>.IndexKeys
            .Ascending(comment => comment.LessonId)
            .Descending(comment => comment.CreatedAt);
        return _comments.Indexes.CreateOneAsync(
            new CreateIndexModel<LessonComment>(keys, new CreateIndexOptions { Name = "lesson_created_desc" }));
    }
}
