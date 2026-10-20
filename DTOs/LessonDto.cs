namespace Educatinal_Platform.DTOs
{
    // 3. DTO للدرس
    public class LessonDto
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string? TrackId { get; set; }
            public int OrderIndex { get; set; }
            public string? ContentText { get; set; }
            // Storage URLs are never exposed to regular clients. Use the access endpoint instead.
            public string? VideoUrl { get; set; }
            public bool HasVideo { get; set; }
            public int VideoDurationSeconds { get; set; }
            public bool IsPreview { get; set; }
            public bool IsWatched { get; set; } // if student has watched it
            public bool IsCompleted { get; set; } // if he completed it
            // These are server-calculated display states; protected endpoints recheck them.
            public bool IsLocked { get; set; }
            public bool IsCurrent { get; set; }
            public List<LessonResourceDto> Resources { get; set; } = new();
        }
    }

