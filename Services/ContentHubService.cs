using Educatinal_Platform.DTOs;
using Educatinal_Platform.Exceptions;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using System.Text;
using System.Text.RegularExpressions;

namespace Educatinal_Platform.Services;

public interface IContentHubService
{
    Task<PagedResult<ContentListDto>> ListVideosAsync(int page, int limit, ContentHubQuery query, bool admin);
    Task<VideoDetailsDto?> GetVideoAsync(string slug, bool admin);
    Task<VideoDetailsDto> CreateVideoAsync(VideoUpsertDto dto);
    Task<VideoDetailsDto> CreateVideoWithThumbnailAsync(VideoUpsertDto dto, IFormFile? thumbnail);
    Task UpdateVideoAsync(string id, VideoUpsertDto dto);
    Task UpdateVideoWithThumbnailAsync(string id, VideoUpsertDto dto, IFormFile? thumbnail);
    Task DeleteVideoAsync(string id);
    Task SetVideoStatusAsync(string id, string status);
    Task<PagedResult<ContentListDto>> ListArticlesAsync(int page, int limit, ContentHubQuery query, bool admin);
    Task<ArticleDetailsDto?> GetArticleAsync(string slug, bool admin);
    Task<ArticleDetailsDto> CreateArticleAsync(ArticleUpsertDto dto);
    Task<ArticleDetailsDto> CreateArticleWithCoverImageAsync(ArticleUpsertDto dto, IFormFile? coverImage);
    Task UpdateArticleAsync(string id, ArticleUpsertDto dto);
    Task UpdateArticleWithCoverImageAsync(string id, ArticleUpsertDto dto, IFormFile? coverImage);
    Task DeleteArticleAsync(string id);
    Task SetArticleStatusAsync(string id, string status);
}

public sealed class ContentHubService(
    IExternalVideoRepository videos,
    IKnowledgeArticleRepository articles,
    ICategoryRepository categories,
    ICourseRepository courses,
    ILessonRepository lessons,
    IFileService files) : IContentHubService
{
    private static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase) { "draft", "published", "unpublished" };
    private static readonly HashSet<string> ArticleTypes = new(StringComparer.OrdinalIgnoreCase) { "article", "study", "case-study", "guide", "analysis", "research", "insight" };

    public async Task<PagedResult<ContentListDto>> ListVideosAsync(int page, int limit, ContentHubQuery query, bool admin)
    {
        ValidatePage(page, limit, query.Search);
        var list = await videos.ListAsync(page, limit, query, !admin);
        var total = await videos.CountAsync(query, !admin);
        return Page(list.Select(ToList).ToList(), total, page, limit);
    }

    public async Task<VideoDetailsDto?> GetVideoAsync(string slug, bool admin)
    {
        if (string.IsNullOrWhiteSpace(slug)) throw new BadRequestException("Video slug is required.");
        var video = await videos.GetBySlugAsync(slug.Trim().ToLowerInvariant(), !admin);
        return video is null ? null : ToDetails(video);
    }

    public Task<VideoDetailsDto> CreateVideoAsync(VideoUpsertDto dto) => CreateVideoWithThumbnailAsync(dto, null);

    public async Task<VideoDetailsDto> CreateVideoWithThumbnailAsync(VideoUpsertDto dto, IFormFile? thumbnail)
    {
        await ValidateVideoAsync(dto);
        string? uploadedThumbnailUrl = null;
        try
        {
            if (thumbnail is not null)
            {
                uploadedThumbnailUrl = await files.UploadImageAsync(thumbnail, "content");
                dto.ThumbnailUrl = uploadedThumbnailUrl;
            }

            var video = new ExternalVideoContent();
            await ApplyAsync(dto, video);
            video.Slug = await UniqueVideoSlugAsync(Slugify(video.Title));
            await videos.CreateAsync(video);
            return ToDetails(video);
        }
        catch
        {
            await DeleteFileQuietlyAsync(uploadedThumbnailUrl);
            throw;
        }
    }

    public Task UpdateVideoAsync(string id, VideoUpsertDto dto) => UpdateVideoWithThumbnailAsync(id, dto, null);

    public async Task UpdateVideoWithThumbnailAsync(string id, VideoUpsertDto dto, IFormFile? thumbnail)
    {
        var video = await videos.GetByIdAsync(id) ?? throw new NotFoundException("Video not found.");
        await ValidateVideoAsync(dto);
        var previousThumbnailUrl = video.ThumbnailUrl;
        string? uploadedThumbnailUrl = null;

        try
        {
            if (thumbnail is not null)
            {
                uploadedThumbnailUrl = await files.UploadImageAsync(thumbnail, "content");
                dto.ThumbnailUrl = uploadedThumbnailUrl;
            }
            else if (dto.ThumbnailUrl is null)
            {
                dto.ThumbnailUrl = previousThumbnailUrl;
            }

            await ApplyAsync(dto, video);
            video.UpdatedAt = DateTime.UtcNow;
            await videos.ReplaceAsync(video);
        }
        catch
        {
            await DeleteFileQuietlyAsync(uploadedThumbnailUrl);
            throw;
        }

        if (uploadedThumbnailUrl is not null && !string.Equals(previousThumbnailUrl, uploadedThumbnailUrl, StringComparison.Ordinal))
            await DeleteFileQuietlyAsync(previousThumbnailUrl);
    }

    public async Task DeleteVideoAsync(string id)
    {
        var video = await videos.GetByIdAsync(id) ?? throw new NotFoundException("Video not found.");
        await videos.DeleteAsync(id);
        await DeleteFileQuietlyAsync(video.ThumbnailUrl);
    }

    public async Task SetVideoStatusAsync(string id, string status)
    {
        var video = await videos.GetByIdAsync(id) ?? throw new NotFoundException("Video not found.");
        ApplyStatus(video, status);
        await videos.ReplaceAsync(video);
    }

    public async Task<PagedResult<ContentListDto>> ListArticlesAsync(int page, int limit, ContentHubQuery query, bool admin)
    {
        ValidatePage(page, limit, query.Search);
        var list = await articles.ListAsync(page, limit, query, !admin);
        var total = await articles.CountAsync(query, !admin);
        return Page(list.Select(ToList).ToList(), total, page, limit);
    }

    public async Task<ArticleDetailsDto?> GetArticleAsync(string slug, bool admin)
    {
        if (string.IsNullOrWhiteSpace(slug)) throw new BadRequestException("Article slug is required.");
        var article = await articles.GetBySlugAsync(slug.Trim().ToLowerInvariant(), !admin);
        return article is null ? null : ToDetails(article);
    }

    public Task<ArticleDetailsDto> CreateArticleAsync(ArticleUpsertDto dto) => CreateArticleWithCoverImageAsync(dto, null);

    public async Task<ArticleDetailsDto> CreateArticleWithCoverImageAsync(ArticleUpsertDto dto, IFormFile? coverImage)
    {
        await ValidateArticleAsync(dto);
        string? uploadedCoverImageUrl = null;
        try
        {
            if (coverImage is not null)
            {
                uploadedCoverImageUrl = await files.UploadImageAsync(coverImage, "content");
                dto.CoverImageUrl = uploadedCoverImageUrl;
            }

            var article = new KnowledgeArticle();
            await ApplyAsync(dto, article);
            article.Slug = await UniqueArticleSlugAsync(string.IsNullOrWhiteSpace(dto.Slug) ? Slugify(article.Title) : Slugify(dto.Slug));
            await articles.CreateAsync(article);
            return ToDetails(article);
        }
        catch
        {
            await DeleteFileQuietlyAsync(uploadedCoverImageUrl);
            throw;
        }
    }

    public Task UpdateArticleAsync(string id, ArticleUpsertDto dto) => UpdateArticleWithCoverImageAsync(id, dto, null);

    public async Task UpdateArticleWithCoverImageAsync(string id, ArticleUpsertDto dto, IFormFile? coverImage)
    {
        var article = await articles.GetByIdAsync(id) ?? throw new NotFoundException("Article not found.");
        await ValidateArticleAsync(dto);
        var previousCoverImageUrl = article.CoverImageUrl;
        string? uploadedCoverImageUrl = null;

        try
        {
            if (coverImage is not null)
            {
                uploadedCoverImageUrl = await files.UploadImageAsync(coverImage, "content");
                dto.CoverImageUrl = uploadedCoverImageUrl;
            }
            else if (dto.CoverImageUrl is null)
            {
                dto.CoverImageUrl = previousCoverImageUrl;
            }

            await ApplyAsync(dto, article);
            if (!string.IsNullOrWhiteSpace(dto.Slug))
            {
                var requestedSlug = Slugify(dto.Slug);
                if (!string.Equals(requestedSlug, article.Slug, StringComparison.Ordinal) && await articles.SlugExistsAsync(requestedSlug, article.Id)) throw new BadRequestException("This article slug is already in use.");
                article.Slug = requestedSlug;
            }
            article.UpdatedAt = DateTime.UtcNow;
            await articles.ReplaceAsync(article);
        }
        catch
        {
            await DeleteFileQuietlyAsync(uploadedCoverImageUrl);
            throw;
        }

        if (uploadedCoverImageUrl is not null && !string.Equals(previousCoverImageUrl, uploadedCoverImageUrl, StringComparison.Ordinal))
            await DeleteFileQuietlyAsync(previousCoverImageUrl);
    }

    public async Task DeleteArticleAsync(string id)
    {
        var article = await articles.GetByIdAsync(id) ?? throw new NotFoundException("Article not found.");
        await articles.DeleteAsync(id);
        await DeleteFileQuietlyAsync(article.CoverImageUrl);
    }

    public async Task SetArticleStatusAsync(string id, string status)
    {
        var article = await articles.GetByIdAsync(id) ?? throw new NotFoundException("Article not found.");
        ApplyStatus(article, status);
        await articles.ReplaceAsync(article);
    }

    private async Task ValidateVideoAsync(VideoUpsertDto dto)
    {
        ValidateText(dto.Title, "Video title", 160, required: true);
        ValidateText(dto.Description, "Video description", 2000, required: false);
        ValidateHttpUrl(dto.Url, "Video URL", required: true);
        ValidateHttpUrl(dto.ThumbnailUrl, "Thumbnail URL", required: false);
        if (!TryBuildEmbedUrl(dto.Url.Trim(), out _, out _)) throw new BadRequestException("The video URL is not a supported public YouTube, Facebook, TikTok, or Vimeo URL.");
        ValidateStatus(dto.Status);
        await ValidateRelationsAsync(dto.CategoryId, dto.RelatedCourseId, dto.RelatedLessonId);
    }

    private async Task ValidateArticleAsync(ArticleUpsertDto dto)
    {
        ValidateText(dto.Title, "Article title", 180, required: true);
        ValidateText(dto.Excerpt, "Excerpt", 500, required: true);
        ValidateText(dto.Subtitle, "Subtitle", 300, required: false);
        ValidateText(dto.Content, "Content", 60000, required: true);
        ValidateText(dto.SeoTitle, "SEO title", 160, required: false);
        ValidateText(dto.SeoDescription, "SEO description", 320, required: false);
        ValidateHttpUrl(dto.CoverImageUrl, "Cover image URL", required: false);
        if (!ArticleTypes.Contains(dto.ContentType ?? string.Empty)) throw new BadRequestException("Content type must be article, study, case-study, guide, analysis, research, or insight.");
        if (ContainsUnsafeMarkup(dto.Content)) throw new BadRequestException("Article content uses HTML that is not permitted. Use the supported safe Markdown syntax instead.");
        ValidateStatus(dto.Status);
        await ValidateRelationsAsync(dto.CategoryId, dto.RelatedCourseId, dto.RelatedLessonId);
    }

    private async Task ValidateRelationsAsync(string? categoryId, string? courseId, string? lessonId)
    {
        if (!string.IsNullOrWhiteSpace(categoryId) && await categories.GetByIdAsync(categoryId.Trim()) is null) throw new BadRequestException("Category not found.");
        if (!string.IsNullOrWhiteSpace(courseId) && await courses.GetByIdAsync(courseId.Trim()) is null) throw new BadRequestException("Related course not found.");
        if (!string.IsNullOrWhiteSpace(lessonId))
        {
            var lesson = await lessons.GetByIdAsync(lessonId.Trim()) ?? throw new BadRequestException("Related lesson not found.");
            if (!string.IsNullOrWhiteSpace(courseId) && !string.Equals(lesson.CourseId, courseId.Trim(), StringComparison.Ordinal)) throw new BadRequestException("The related lesson does not belong to the selected course.");
        }
    }

    private async Task ApplyAsync(VideoUpsertDto dto, ExternalVideoContent target)
    {
        // Async relationship validation happens before this method; this method only copies normalized values.
        if (!TryBuildEmbedUrl(dto.Url.Trim(), out var platform, out var embedUrl)) throw new BadRequestException("Unsupported video URL.");
        target.Title = dto.Title.Trim(); target.Url = dto.Url.Trim(); target.Platform = platform; target.EmbedUrl = embedUrl;
        target.Description = NullIfBlank(dto.Description); target.ThumbnailUrl = NullIfBlank(dto.ThumbnailUrl); target.CategoryId = NullIfBlank(dto.CategoryId);
        target.CategoryName = target.CategoryId is null ? null : (await categories.GetByIdAsync(target.CategoryId))?.Name;
        target.Tags = NormalizeTags(dto.Tags); target.IsFeatured = dto.IsFeatured; target.RelatedCourseId = NullIfBlank(dto.RelatedCourseId); target.RelatedLessonId = NullIfBlank(dto.RelatedLessonId);
        ApplyStatus(target, dto.Status ?? target.Status);
    }

    private async Task ApplyAsync(ArticleUpsertDto dto, KnowledgeArticle target)
    {
        target.Title = dto.Title.Trim(); target.Subtitle = NullIfBlank(dto.Subtitle); target.Excerpt = dto.Excerpt.Trim(); target.CoverImageUrl = NullIfBlank(dto.CoverImageUrl); target.Content = dto.Content.Trim(); target.ContentType = dto.ContentType.Trim().ToLowerInvariant();
        target.CategoryId = NullIfBlank(dto.CategoryId); target.CategoryName = target.CategoryId is null ? null : (await categories.GetByIdAsync(target.CategoryId))?.Name;
        target.Tags = NormalizeTags(dto.Tags); target.IsFeatured = dto.IsFeatured; target.RelatedCourseId = NullIfBlank(dto.RelatedCourseId); target.RelatedLessonId = NullIfBlank(dto.RelatedLessonId); target.SeoTitle = NullIfBlank(dto.SeoTitle); target.SeoDescription = NullIfBlank(dto.SeoDescription);
        target.ReadingTimeMinutes = Math.Max(1, (int)Math.Ceiling(Regex.Matches(target.Content, @"\S+").Count / 200d));
        ApplyStatus(target, dto.Status ?? target.Status);
    }

    private async Task DeleteFileQuietlyAsync(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return;
        try { await files.DeleteAsync(fileUrl); } catch { }
    }

    private static void ApplyStatus(ExternalVideoContent target, string status) { target.Status = NormalizeStatus(status); if (target.Status == "published" && target.PublishedAt is null) target.PublishedAt = DateTime.UtcNow; if (target.Status != "published") target.PublishedAt = null; target.UpdatedAt = DateTime.UtcNow; }
    private static void ApplyStatus(KnowledgeArticle target, string status) { target.Status = NormalizeStatus(status); if (target.Status == "published" && target.PublishedAt is null) target.PublishedAt = DateTime.UtcNow; if (target.Status != "published") target.PublishedAt = null; target.UpdatedAt = DateTime.UtcNow; }
    private static void ValidateStatus(string? status) { if (status is not null && !Statuses.Contains(status)) throw new BadRequestException("Status must be draft, published, or unpublished."); }
    private static string NormalizeStatus(string status) { ValidateStatus(status); return status.Trim().ToLowerInvariant(); }
    private static void ValidatePage(int page, int limit, string? search) { if (page < 1) throw new BadRequestException("Page number must be greater than zero."); if (limit is < 1 or > 50) throw new BadRequestException("Limit must be between 1 and 50."); if (search?.Length > 100) throw new BadRequestException("Search text must be 100 characters or fewer."); }
    private static void ValidateText(string? value, string label, int max, bool required) { if (required && string.IsNullOrWhiteSpace(value)) throw new BadRequestException($"{label} is required."); if (value?.Length > max) throw new BadRequestException($"{label} must be {max} characters or fewer."); }
    private static void ValidateHttpUrl(string? value, string label, bool required) { if (required && string.IsNullOrWhiteSpace(value)) throw new BadRequestException($"{label} is required."); if (!string.IsNullOrWhiteSpace(value) && (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))) throw new BadRequestException($"{label} must be a valid HTTP or HTTPS URL."); }
    private static bool ContainsUnsafeMarkup(string text) => Regex.IsMatch(text, @"<\s*/?\s*[a-z!]|javascript\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static List<string> NormalizeTags(List<string>? tags) => (tags ?? new()).Select(x => x.Trim()).Where(x => x.Length is > 0 and <= 50).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
    private async Task<string> UniqueVideoSlugAsync(string initial) { var slug = initial; var number = 2; while (await videos.SlugExistsAsync(slug)) slug = $"{initial}-{number++}"; return slug; }
    private async Task<string> UniqueArticleSlugAsync(string initial) { var slug = initial; var number = 2; while (await articles.SlugExistsAsync(slug)) slug = $"{initial}-{number++}"; return slug; }
    private static string Slugify(string source) { var normalized = source.Trim().ToLowerInvariant(); normalized = Regex.Replace(normalized, @"[^\p{L}\p{N}]+", "-"); return normalized.Trim('-') is { Length: > 0 } slug ? slug : "content"; }
    private static PagedResult<ContentListDto> Page(List<ContentListDto> items, long total, int page, int limit) => new() { Items = items, TotalItems = (int)total, TotalPages = (int)Math.Ceiling(total / (double)limit), CurrentPage = page, PageSize = limit };
    private static ContentListDto ToList(ExternalVideoContent x) => new() { Id = x.Id, Title = x.Title, Slug = x.Slug, Description = x.Description, ThumbnailUrl = x.ThumbnailUrl, CategoryId = x.CategoryId, CategoryName = x.CategoryName, Tags = x.Tags, IsFeatured = x.IsFeatured, Status = x.Status, Platform = x.Platform, PublishedAt = x.PublishedAt, CreatedAt = x.CreatedAt };
    private static ContentListDto ToList(KnowledgeArticle x) => new() { Id = x.Id, Title = x.Title, Slug = x.Slug, Description = x.Excerpt, ThumbnailUrl = x.CoverImageUrl, CategoryId = x.CategoryId, CategoryName = x.CategoryName, Tags = x.Tags, IsFeatured = x.IsFeatured, Status = x.Status, ContentType = x.ContentType, ReadingTimeMinutes = x.ReadingTimeMinutes, PublishedAt = x.PublishedAt, CreatedAt = x.CreatedAt };
    private static VideoDetailsDto ToDetails(ExternalVideoContent x) => new() { Id = x.Id, Title = x.Title, Slug = x.Slug, Description = x.Description, ThumbnailUrl = x.ThumbnailUrl, CategoryId = x.CategoryId, CategoryName = x.CategoryName, Tags = x.Tags, IsFeatured = x.IsFeatured, Status = x.Status, Platform = x.Platform, PublishedAt = x.PublishedAt, CreatedAt = x.CreatedAt, Url = x.Url, EmbedUrl = x.EmbedUrl, RelatedCourseId = x.RelatedCourseId, RelatedLessonId = x.RelatedLessonId };
    private static ArticleDetailsDto ToDetails(KnowledgeArticle x) => new() { Id = x.Id, Title = x.Title, Slug = x.Slug, Description = x.Excerpt, ThumbnailUrl = x.CoverImageUrl, CategoryId = x.CategoryId, CategoryName = x.CategoryName, Tags = x.Tags, IsFeatured = x.IsFeatured, Status = x.Status, ContentType = x.ContentType, ReadingTimeMinutes = x.ReadingTimeMinutes, PublishedAt = x.PublishedAt, CreatedAt = x.CreatedAt, Subtitle = x.Subtitle, Content = x.Content, RelatedCourseId = x.RelatedCourseId, RelatedLessonId = x.RelatedLessonId, SeoTitle = x.SeoTitle, SeoDescription = x.SeoDescription };

    public static bool TryBuildEmbedUrl(string rawUrl, out string platform, out string embedUrl)
    {
        platform = string.Empty; embedUrl = string.Empty;
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return false;
        var host = uri.Host.ToLowerInvariant(); var path = uri.AbsolutePath.Trim('/');
        if (IsHost(host, "youtube.com") || host == "youtu.be")
        {
            var queryVideoId = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(pair => pair.Split('=', 2)).FirstOrDefault(pair => pair.Length == 2 && pair[0] == "v")?.ElementAtOrDefault(1);
            var id = host == "youtu.be" ? path.Split('/')[0] : queryVideoId ?? path.Split('/').SkipWhile(x => x is not "embed" and not "shorts").Skip(1).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, @"^[A-Za-z0-9_-]{6,}$")) return false; platform = "youtube"; embedUrl = $"https://www.youtube.com/embed/{id}"; return true;
        }
        if (IsHost(host, "vimeo.com")) { var id = path.Split('/').LastOrDefault(); if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, @"^\d+$")) return false; platform = "vimeo"; embedUrl = $"https://player.vimeo.com/video/{id}"; return true; }
        if (IsHost(host, "tiktok.com")) { var id = path.Split('/').LastOrDefault(x => Regex.IsMatch(x, @"^\d+$")); if (id is null) return false; platform = "tiktok"; embedUrl = $"https://www.tiktok.com/embed/v2/{id}"; return true; }
        if (IsHost(host, "facebook.com") || host == "fb.watch") { platform = "facebook"; embedUrl = $"https://www.facebook.com/plugins/video.php?href={Uri.EscapeDataString(rawUrl)}&show_text=false"; return true; }
        return false;
    }
    private static bool IsHost(string host, string domain) => host == domain || host.EndsWith($".{domain}", StringComparison.Ordinal);
}
