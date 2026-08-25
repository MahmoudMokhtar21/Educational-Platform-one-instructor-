namespace Educatinal_Platform.DTOs
{
    public class WatchedLessonDto
    {
        public string LessonId { get; set; } = string.Empty;
        public string LessonTitle { get; set; } = string.Empty;
        public DateTime WatchedAt { get; set; }
        public int WatchDurationSeconds { get; set; }
        public bool IsCompleted { get; set; }
    }

  
}
