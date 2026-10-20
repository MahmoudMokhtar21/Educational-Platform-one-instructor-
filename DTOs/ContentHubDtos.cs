namespace Educatinal_Platform.DTOs;

public sealed class VideoUpsertDto
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public IFormFile? Thumbnail { get; set; }
    public string? CategoryId { get; set; }
    public List<string>? Tags { get; set; }
    public bool IsFeatured { get; set; }
    public string? Status { get; set; }
    public string? RelatedCourseId { get; set; }
    public string? RelatedLessonId { get; set; }
}

public sealed class ArticleUpsertDto
{
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Subtitle { get; set; }
    public string Excerpt { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public IFormFile? CoverImage { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "article";
    public string? CategoryId { get; set; }
    public List<string>? Tags { get; set; }
    public bool IsFeatured { get; set; }
    public string? Status { get; set; }
    public string? RelatedCourseId { get; set; }
    public string? RelatedLessonId { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
}

public class ContentListDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsFeatured { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? ContentType { get; set; }
    public int? ReadingTimeMinutes { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class VideoDetailsDto : ContentListDto
{
    public string Url { get; set; } = string.Empty;
    public string EmbedUrl { get; set; } = string.Empty;
    public string? RelatedCourseId { get; set; }
    public string? RelatedLessonId { get; set; }
}

public sealed class ArticleDetailsDto : ContentListDto
{
    public string? Subtitle { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? RelatedCourseId { get; set; }
    public string? RelatedLessonId { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
}
