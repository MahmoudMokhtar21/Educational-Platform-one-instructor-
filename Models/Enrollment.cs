using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Educatinal_Platform.Models
{
    public class Enrollment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        public string StudentId { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;

        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public decimal ProgressPercent { get; set; } = 0;

        public List<WatchedLesson> WatchedLessons { get; set; } = new();

        public string? LastWatchedLessonId { get; set; }
        public DateTime? LastWatchedAt { get; set; }

        public string Status { get; set; } = "active"; // active, completed, cancelled

        public decimal? FinalExamScorePercent { get; set; }
        public bool IsCertificateIssued { get; set; } = false;
    }

    public class WatchedLesson
    {
        public string LessonId { get; set; } = string.Empty;
        public string LessonTitle { get; set; } = string.Empty;
        public DateTime WatchedAt { get; set; } = DateTime.UtcNow;
        public int WatchDurationSeconds { get; set; } = 0;
        public bool IsCompleted { get; set; } = false;
    }
}
