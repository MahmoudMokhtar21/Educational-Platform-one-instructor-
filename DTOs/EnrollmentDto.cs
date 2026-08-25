namespace Educatinal_Platform.DTOs
{
    public class EnrollmentDto
    {
        public string Id { get; set; } = string.Empty;

        public string CourseId { get; set; } = string.Empty;

        public DateTime EnrolledAt { get; set; }

        public decimal ProgressPercent { get; set; }

        public string? LastWatchedLessonId { get; set; }

        public DateTime? LastWatchedAt { get; set; }

        public string Status { get; set; } = string.Empty;

        public bool IsCertificateIssued { get; set; }

        public List<WatchedLessonDto> WatchedLessons { get; set; }
            = new();
    }

  
}
