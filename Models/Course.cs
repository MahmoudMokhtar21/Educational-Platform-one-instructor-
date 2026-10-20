using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Educatinal_Platform.Models
{
    public class Course
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty; // زي "aspnet-core-course"
        public string Description { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public decimal Price { get; set; } = 0; // Legacy/basic price retained for existing consumers.
        public decimal? BasicPrice { get; set; }
        public decimal? FullAccessPrice { get; set; }

        public string? ThumbnailUrl { get; set; }
        public string? TrailerUrl { get; set; }


        public string Level { get; set; } = "beginner"; // beginner, intermediate, advanced
        public decimal TotalHours { get; set; } = 0;
        public int TotalLessons { get; set; } = 0;

        public string Status { get; set; } = "draft"; // draft, published, archived

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Kept as the primary category for legacy MongoDB documents and API consumers.
        // New courses also persist every assignment in Categories.
        public CategoryEmbedded? Category { get; set; }

        // A course may belong to more than one category. Existing documents that only
        // contain Category remain valid and are normalized by CourseService when read.
        public List<CategoryEmbedded> Categories { get; set; } = new();

        public List<string> Tags { get; set; } = new();

        public int TotalStudents { get; set; } = 0;
        public decimal AverageRating { get; set; } = 0;
        public int TotalReviews { get; set; } = 0;
    }
    public class CategoryEmbedded
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
    }

}
