using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Educatinal_Platform.Models;

/// <summary>
/// Long-form internal knowledge content. Body uses a safe Markdown subset,
/// rather than arbitrary HTML, so it can be rendered without script injection.
/// </summary>
public class KnowledgeArticle
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Excerpt { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "article";
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsFeatured { get; set; }
    public string Status { get; set; } = "draft";
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int ReadingTimeMinutes { get; set; }
    public string? RelatedCourseId { get; set; }
    public string? RelatedLessonId { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
}
