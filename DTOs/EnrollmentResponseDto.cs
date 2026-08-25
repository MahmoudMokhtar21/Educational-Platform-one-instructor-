namespace Educatinal_Platform.DTOs
{
    public class EnrollmentResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;

        // course data (embadded)
        public string CourseTitle { get; set; } = string.Empty;
        public string CourseSlug { get; set; } = string.Empty;
        public string? CourseThumbnail { get; set; }
        public decimal CoursePrice { get; set; }

        // enrollment state
        public DateTime EnrolledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public decimal ProgressPercent { get; set; }
        public string Status { get; set; } = string.Empty; // active, completed, cancelled

        // additional info
        public int TotalLessons { get; set; }
        public int CompletedLessons { get; set; }
        public decimal? FinalExamScore { get; set; }
        public bool HasCertificate { get; set; }
    }
    public class EnrollmentDetailDto : EnrollmentResponseDto
    {
        //student data for admin
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;

        //WatchedLessonDto
        public List<WatchedLessonDto> WatchedLessons { get; set; } = new();

        // Last Activity At
        public DateTime? LastActivityAt { get; set; }
        public string? LastWatchedLessonTitle { get; set; }

        // payment
        public decimal? PaidAmount { get; set; }
        public string? PaymentStatus { get; set; }
        public DateTime? PaymentDate { get; set; }
    }

  
}
