using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs
{
    public class TrackDto
    {
        public string Id { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<LessonDto> Lessons { get; set; } = new();
        public int LessonCount => Lessons.Count;
    }

    public class CreateTrackDto
    {
        [Required]
        [MinLength(2)]
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateTrackDto
    {
        [MinLength(2)]
        public string? Title { get; set; }
        public string? Description { get; set; }
    }

    public class ReorderTracksDto
    {
        [Required]
        [MinLength(1)]
        public List<TrackOrderDto> Tracks { get; set; } = new();
    }

    public class TrackOrderDto
    {
        [Required]
        public string TrackId { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int OrderIndex { get; set; }
    }

    public class MoveLessonToTrackDto
    {
        [Required]
        public string TrackId { get; set; } = string.Empty;
    }
}
