using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Educatinal_Platform.Models;

/// <summary>
/// A learner-authored, immutable comment attached to a single lesson.
/// Author fields are captured by the server from the authenticated user record;
/// they are never accepted from the browser.
/// </summary>
public sealed class LessonComment
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string LessonId { get; set; } = string.Empty;

    public string AuthorId { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string? AuthorProfilePictureUrl { get; set; }

    /// <summary>
    /// Plain text only. The frontend renders it as text rather than HTML.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
