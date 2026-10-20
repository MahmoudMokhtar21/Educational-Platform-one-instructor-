namespace Educatinal_Platform.DTOs
{
    public class CourseDetailDto : CourseResponseDto
    {
        public string Description { get; set; } = string.Empty;
        public List<LessonDto> Lessons { get; set; } = new();
        public List<TrackDto> Tracks { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public bool IsEnrolled { get; set; }
        // Administrative preview is intentionally separate from enrollment.
        // It never changes course payment, enrollment, or review eligibility.
        public bool HasAdminAccess { get; set; }
        public decimal? ProgressPercent { get; set; }
        // Filled from the requesting student enrollment; never read from the browser.
        public string? AccessType { get; set; }
    }

    public class CategoryEmbeddedDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
    }
}
