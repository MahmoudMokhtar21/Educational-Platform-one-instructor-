using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs
{
    public class ReorderLessonsDto
    {
        [Required]
        [MinLength(1)]
        public List<LessonOrderDto> Lessons { get; set; } = new();
    }

    public class LessonOrderDto
    {
        [Required]
        public string LessonId { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int OrderIndex { get; set; }
    }
}
