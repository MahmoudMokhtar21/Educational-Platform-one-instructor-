using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs
{
    public class CreateLessonDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        public string? ContentText { get; set; }

        public IFormFile? Video { get; set; }

        [Range(0, int.MaxValue)]
        public int VideoDurationSeconds { get; set; }

        public bool IsPreview { get; set; } = false;

        public List<LessonResourceDto> Resources { get; set; } = new();
    }

}
