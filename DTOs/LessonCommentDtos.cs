using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs;

public sealed class CreateLessonCommentDto
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}

public sealed class LessonCommentResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorProfilePictureUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
