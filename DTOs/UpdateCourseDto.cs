using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs
{
    // DTO for admin course updates.
    public class UpdateCourseDto
    {
        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(5000)]
        public string Description { get; set; } = string.Empty;

        [StringLength(300)]
        public string? ShortDescription { get; set; }

        [Range(0, 100000)]
        public decimal Price { get; set; }

        [Range(0, 100000)]
        public decimal? BasicPrice { get; set; }

        [Range(0, 100000)]
        public decimal? FullAccessPrice { get; set; }

        [Required]
        public string Level { get; set; } = "beginner";

        public string? Status { get; set; } // draft, published, archived
        public List<string>? Tags { get; set; }

        // CategoryId remains supported for older clients that update one category.
        public string? CategoryId { get; set; }

        // One form value per selected category. With ReplaceCategories=true an empty
        // list deliberately clears all assignments.
        public List<string>? CategoryIds { get; set; }
        public bool? ReplaceCategories { get; set; }

        public IFormFile? Thumbnail { get; set; }
        public IFormFile? Trailer { get; set; }
    }
}
