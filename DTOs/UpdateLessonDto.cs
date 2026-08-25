using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs
{
    public class UpdateLessonDto
    {
        [MinLength(2)]
        public string? Title { get; set; }

        public string? ContentText { get; set; }

        public IFormFile? Video { get; set; }

        [Range(0, int.MaxValue)]
        public int? VideoDurationSeconds { get; set; }

        public bool? IsPreview { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<LessonResourceDto>? Resources { get; set; }
    }
}
