using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs
{
    // 5. DTO لتحديث كورس (للأدمن)
    public class UpdateCourseDto
        {
            [Required]
            [StringLength(150)]
            public string Title { get; set; } = string.Empty;

            [Required]
            [StringLength(5000)]
            public string Description { get; set; } = string.Empty;

            [Range(0, 100000)]
            public decimal Price { get; set; }

            [Required]
            public string Level { get; set; } = "beginner";
       
            public string? Status { get; set; } // draft, published, archived
            public List<string>? Tags { get; set; }
            public IFormFile? Thumbnail { get; set; }
            public IFormFile? Trailer { get; set; }
        }
    }

