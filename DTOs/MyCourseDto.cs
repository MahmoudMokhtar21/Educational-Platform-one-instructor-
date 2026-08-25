namespace Educatinal_Platform.DTOs
{
    public class MyCourseDto
    {
        public string EnrollmentId { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string CourseSlug { get; set; } = string.Empty;
        public string? CourseThumbnail { get; set; }
        public decimal ProgressPercent { get; set; }
        public string Status { get; set; } = string.Empty; // active, completed
        public DateTime EnrolledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalLessons { get; set; }
        public int CompletedLessons { get; set; }
    }
}

