namespace Educatinal_Platform.DTOs;

/// <summary>
/// A time-limited browser URL for a lesson asset. It is never a storage path and
/// is issued only after the server has authorized the requested content.
/// </summary>
public sealed class LessonContentAccessDto
{
    public string Url { get; init; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; init; }
}
