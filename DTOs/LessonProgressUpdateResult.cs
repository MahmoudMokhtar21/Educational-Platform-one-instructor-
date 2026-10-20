namespace Educatinal_Platform.DTOs;

public sealed class LessonProgressUpdateResult
{
    public int WatchDurationSeconds { get; init; }
    public bool IsCompleted { get; init; }
    public decimal ProgressPercent { get; init; }
    public string AccessType { get; init; } = string.Empty;
    public string? NextLessonId { get; init; }
}
