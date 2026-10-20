using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs
{
    public class CreateCourseDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [StringLength(300)]
        public string? ShortDescription { get; set; }

        public decimal Price { get; set; } = 0;
        public decimal? BasicPrice { get; set; }
        public decimal? FullAccessPrice { get; set; }
        public string Level { get; set; } = "beginner";

        // CategoryId remains accepted for legacy multipart clients.
        public string? CategoryId { get; set; }

        // New clients send one CategoryIds value per selected category.
        public List<string>? CategoryIds { get; set; }

        public List<string> Tags { get; set; } = new();
        public IFormFile? Thumbnail { get; set; }
        public IFormFile? Trailer { get; set; }
    }
}
