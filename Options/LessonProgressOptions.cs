namespace Educatinal_Platform.Options;

/// <summary>
/// Centralized, configurable video-progress rules. They are deliberately
/// forgiving enough for normal playback and seeking, while preventing one
/// request from turning an untouched long video into a completed lesson.
/// </summary>
public sealed class LessonProgressOptions
{
    public const string SectionName = "LessonProgress";

    public int VideoCompletionPercent { get; set; } = 90;
    public decimal MaximumPlaybackRate { get; set; } = 1.5m;
    public int InitialProgressAllowanceSeconds { get; set; } = 15;
}
