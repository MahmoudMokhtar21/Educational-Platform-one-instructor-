namespace Educatinal_Platform.DTOs
{
    public class EnrollmentProgressDto
    {
        public string EnrollmentId { get; set; } = string.Empty;
        public decimal ProgressPercent { get; set; }
        public int CompletedLessons { get; set; }
        public int TotalLessons { get; set; }
        public string? LastWatchedLessonId { get; set; }
        public DateTime? LastWatchedAt { get; set; }

        // if he has finished the course
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

  
}
