using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Educatinal_Platform.Models
{
    public class Lesson
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        public string CourseId { get; set; } = string.Empty; 
        public int OrderIndex { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ContentText { get; set; }
        public string? VideoUrl { get; set; }
        public int VideoDurationSeconds { get; set; }
        public bool IsPreview { get; set; } = false; 

        public List<LessonResource> Resources { get; set; } = new(); 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    }
    public class LessonResource
    {
        public string Id { get; set; } =
       ObjectId.GenerateNewId().ToString();
        public string Title { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty; // pdf, zip, docx
    }
}
