namespace Educatinal_Platform.DTOs
{
    // 3. DTO للدرس
    public class LessonDto
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public int OrderIndex { get; set; }
            public string? VideoUrl { get; set; }
            public int VideoDurationSeconds { get; set; }
            public bool IsPreview { get; set; }
            public bool IsWatched { get; set; } // if student has watched it
            public bool IsCompleted { get; set; } // if he completed it
        }
    }

