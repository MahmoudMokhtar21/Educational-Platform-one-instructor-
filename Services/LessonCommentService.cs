using System.Security.Claims;
using Educatinal_Platform.DTOs;
using Educatinal_Platform.Exceptions;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using MongoDB.Driver;

namespace Educatinal_Platform.Services;

public interface ILessonCommentService
{
    Task<List<LessonCommentResponseDto>> GetByLessonAsync(ClaimsPrincipal principal, string lessonId);
    Task<LessonCommentResponseDto> CreateAsync(ClaimsPrincipal principal, string lessonId, CreateLessonCommentDto dto);
}

/// <summary>
/// Keeps lesson-comment authorization aligned with protected lesson content:
/// admins have access, published previews are available to authenticated users,
/// and all other lessons require the current enrollment entitlement.
/// </summary>
public sealed class LessonCommentService : ILessonCommentService
{
    public const int MaximumCommentLength = 2000;

    private readonly ILessonCommentRepository _commentRepository;
    private readonly ILessonRepository _lessonRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ICourseLessonAccessService _courseLessonAccessService;
    private readonly IMongoCollection<User> _users;

    public LessonCommentService(
        ILessonCommentRepository commentRepository,
        ILessonRepository lessonRepository,
        ICourseRepository courseRepository,
        ICourseLessonAccessService courseLessonAccessService,
        IMongoDatabase database)
    {
        _commentRepository = commentRepository;
        _lessonRepository = lessonRepository;
        _courseRepository = courseRepository;
        _courseLessonAccessService = courseLessonAccessService;
        _users = database.GetCollection<User>("Users");
    }

    public async Task<List<LessonCommentResponseDto>> GetByLessonAsync(ClaimsPrincipal principal, string lessonId)
    {
        await EnsureLessonAccessAsync(principal, lessonId);
        var comments = await _commentRepository.GetByLessonIdAsync(lessonId);
        return comments.Select(MapResponse).ToList();
    }

    public async Task<LessonCommentResponseDto> CreateAsync(
        ClaimsPrincipal principal,
        string lessonId,
        CreateLessonCommentDto dto)
    {
        await EnsureLessonAccessAsync(principal, lessonId);
        var authorId = GetAuthenticatedUserId(principal);
        var content = NormalizeContent(dto.Content);

        var author = await _users
            .Find(user => user.Id == authorId)
            .FirstOrDefaultAsync();
        if (author is null)
            throw new UnauthorizedException("The authenticated account could not be found.");

        var authorName = string.Join(" ", new[] { author.FirstName, author.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        if (string.IsNullOrWhiteSpace(authorName))
            authorName = "Learner";

        var comment = new LessonComment
        {
            LessonId = lessonId,
            AuthorId = authorId,
            AuthorName = authorName,
            AuthorProfilePictureUrl = string.IsNullOrWhiteSpace(author.ProfilePictureUrl)
                ? null
                : author.ProfilePictureUrl.Trim(),
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _commentRepository.CreateAsync(comment);
        return MapResponse(created);
    }

    private async Task<Lesson> EnsureLessonAccessAsync(ClaimsPrincipal principal, string lessonId)
    {
        if (string.IsNullOrWhiteSpace(lessonId))
            throw new BadRequestException("Lesson id is required.");

        var lesson = await _lessonRepository.GetByIdAsync(lessonId);
        if (lesson is null)
            throw new NotFoundException("Lesson not found.");

        if (principal.IsInRole("admin"))
            return lesson;

        var course = await _courseRepository.GetByIdAsync(lesson.CourseId);
        if (course is null || !string.Equals(course.Status, "published", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("This lesson is not available for comments.");

        // A preview is public lesson content, but the endpoint itself is still
        // authenticated so comments always have a verified server-side author.
        if (lesson.IsPreview)
            return lesson;

        var access = await _courseLessonAccessService.CanAccessAsync(
            GetAuthenticatedUserId(principal),
            lesson.CourseId,
            lesson.Id);
        if (!access.IsAllowed)
            throw new ForbiddenException(access.DenialMessage ?? "You do not have access to this lesson.");

        return lesson;
    }

    private static string GetAuthenticatedUserId(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedException("Authentication is required.");

        return userId;
    }

    private static string NormalizeContent(string? value)
    {
        var content = (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        if (string.IsNullOrWhiteSpace(content))
            throw new BadRequestException("Comment content is required.");
        if (content.Length > MaximumCommentLength)
            throw new BadRequestException($"Comment cannot exceed {MaximumCommentLength} characters.");
        if (content.Any(character => char.IsControl(character) && character != '\n' && character != '\t'))
            throw new BadRequestException("Comment contains unsupported control characters.");

        return content;
    }

    private static LessonCommentResponseDto MapResponse(LessonComment comment) => new()
    {
        Id = comment.Id,
        AuthorName = comment.AuthorName,
        AuthorProfilePictureUrl = comment.AuthorProfilePictureUrl,
        Content = comment.Content,
        CreatedAt = comment.CreatedAt
    };
}
