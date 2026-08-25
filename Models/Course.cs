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
        public decimal Price { get; set; } = 0; // 0 = free 

        public string? ThumbnailUrl { get; set; }
        public string? TrailerUrl { get; set; }


        public string Level { get; set; } = "beginner"; // beginner, intermediate, advanced
        public decimal TotalHours { get; set; } = 0;
        public int TotalLessons { get; set; } = 0;

        public string Status { get; set; } = "draft"; // draft, published, archived

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

       
        public CategoryEmbedded? Category { get; set; }

       
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
