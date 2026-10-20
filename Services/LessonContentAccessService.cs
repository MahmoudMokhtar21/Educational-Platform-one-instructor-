using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Educatinal_Platform.DTOs;
using Educatinal_Platform.Exceptions;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Educatinal_Platform.Services;

public interface ILessonContentAccessService
{
    Task<LessonContentAccessDto?> GetVideoAccessAsync(ClaimsPrincipal principal, string lessonId);
    Task<LessonContentAccessDto?> GetResourceAccessAsync(ClaimsPrincipal principal, string lessonId, string resourceId);
    Task<ProtectedLessonContent?> OpenVideoAsync(string lessonId, string? accessToken);
    Task<ProtectedLessonContent?> OpenResourceAsync(string lessonId, string resourceId, string? accessToken);
}

public sealed record ProtectedLessonContent(string PhysicalPath, string ContentType);

/// <summary>
/// Issues narrowly scoped, expiring URLs for platform-hosted lesson media and
/// rechecks the current course access rules whenever a file is streamed.
/// </summary>
public sealed class LessonContentAccessService : ILessonContentAccessService
{
    private const string ContentTypeClaim = "lesson-content:type";
    private const string LessonIdClaim = "lesson-content:lesson-id";
    private const string ResourceIdClaim = "lesson-content:resource-id";
    private const string UserIdClaim = "lesson-content:user-id";
    private const string AdminClaim = "lesson-content:is-admin";
    private const string VideoContentType = "video";
    private const string ResourceContentType = "resource";
    private const string LessonUploadsPrefix = "/uploads/lessons/";

    private readonly ILessonRepository _lessonRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ICourseLessonAccessService _courseLessonAccessService;
    private readonly IWebHostEnvironment _environment;

    private readonly SigningCredentials _signingCredentials;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TokenValidationParameters _tokenValidationParameters;
    private readonly TimeSpan _accessUrlLifetime;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public LessonContentAccessService(
        ILessonRepository lessonRepository,
        ICourseRepository courseRepository,
        ICourseLessonAccessService courseLessonAccessService,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _lessonRepository = lessonRepository;
        _courseRepository = courseRepository;
        _courseLessonAccessService = courseLessonAccessService;
        _environment = environment;

        var secret = configuration["JWT:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("JWT:SecretKey must be configured before protected lesson content can be served.");

        var lifetimeMinutes = configuration.GetValue<int?>("LessonContentAccess:AccessUrlLifetimeMinutes") ?? 30;
        if (lifetimeMinutes is < 1 or > 120)
            throw new InvalidOperationException("LessonContentAccess:AccessUrlLifetimeMinutes must be between 1 and 120.");

        _accessUrlLifetime = TimeSpan.FromMinutes(lifetimeMinutes);
        _issuer = configuration["JWT:Issuer"] ?? throw new InvalidOperationException("JWT:Issuer must be configured.");
        _audience = configuration["JWT:Audience"] ?? throw new InvalidOperationException("JWT:Audience must be configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }

    public async Task<LessonContentAccessDto?> GetVideoAccessAsync(ClaimsPrincipal principal, string lessonId)
    {
        var lesson = await _lessonRepository.GetByIdAsync(lessonId);
        if (lesson == null || string.IsNullOrWhiteSpace(lesson.VideoUrl) || !IsPlatformLessonFile(lesson.VideoUrl))
            return null;

        var subject = GetSubject(principal);
        var authorization = await AuthorizeVideoAsync(lesson, subject);
        if (!authorization.IsAllowed)
        {
            ThrowIfLocked(authorization);
            return null;
        }

        return CreateAccessUrl(lesson.Id, resourceId: null, VideoContentType, subject,
            $"/api/lessons/{Uri.EscapeDataString(lesson.Id)}/video");
    }

    public async Task<LessonContentAccessDto?> GetResourceAccessAsync(ClaimsPrincipal principal, string lessonId, string resourceId)
    {
        var lesson = await _lessonRepository.GetByIdAsync(lessonId);
        var resource = lesson?.Resources.FirstOrDefault(item => item.Id == resourceId);
        if (lesson == null || resource == null)
            return null;

        var authorization = await AuthorizeResourceAsync(lesson, GetSubject(principal));
        if (!authorization.IsAllowed)
        {
            ThrowIfLocked(authorization);
            return null;
        }

        if (!IsPlatformLessonFile(resource.FileUrl))
        {
            return IsSafeExternalUrl(resource.FileUrl)
                ? new LessonContentAccessDto { Url = resource.FileUrl }
                : null;
        }

        return CreateAccessUrl(lesson.Id, resource.Id, ResourceContentType, GetSubject(principal),
            $"/api/lessons/{Uri.EscapeDataString(lesson.Id)}/resources/{Uri.EscapeDataString(resource.Id)}");
    }

    public async Task<ProtectedLessonContent?> OpenVideoAsync(string lessonId, string? accessToken)
    {
        var ticket = await ValidateAccessTokenAsync(accessToken);
        if (ticket == null || ticket.ContentType != VideoContentType || ticket.ResourceId != null ||
            !string.Equals(ticket.LessonId, lessonId, StringComparison.Ordinal))
            return null;

        var lesson = await _lessonRepository.GetByIdAsync(lessonId);
        if (lesson == null || string.IsNullOrWhiteSpace(lesson.VideoUrl))
            return null;

        var authorization = await AuthorizeVideoAsync(lesson, ticket.Subject);
        if (!authorization.IsAllowed)
        {
            ThrowIfLocked(authorization);
            return null;
        }

        return ResolvePlatformFile(lesson.VideoUrl);
    }

    public async Task<ProtectedLessonContent?> OpenResourceAsync(string lessonId, string resourceId, string? accessToken)
    {
        var ticket = await ValidateAccessTokenAsync(accessToken);
        if (ticket == null || ticket.ContentType != ResourceContentType ||
            !string.Equals(ticket.LessonId, lessonId, StringComparison.Ordinal) ||
            !string.Equals(ticket.ResourceId, resourceId, StringComparison.Ordinal))
            return null;

        var lesson = await _lessonRepository.GetByIdAsync(lessonId);
        var resource = lesson?.Resources.FirstOrDefault(item => item.Id == resourceId);
        if (lesson == null || resource == null)
            return null;

        var authorization = await AuthorizeResourceAsync(lesson, ticket.Subject);
        if (!authorization.IsAllowed)
        {
            ThrowIfLocked(authorization);
            return null;
        }

        return ResolvePlatformFile(resource.FileUrl);
    }

    private async Task<ContentAuthorization> AuthorizeVideoAsync(Lesson lesson, AccessSubject subject)
    {
        if (subject.IsAdmin)
            return ContentAuthorization.Allowed;

        var course = await _courseRepository.GetByIdAsync(lesson.CourseId);
        if (course == null || !string.Equals(course.Status, "published", StringComparison.OrdinalIgnoreCase))
            return ContentAuthorization.Denied;

        // Preserve public preview playback independently from enrollment plans.
        if (lesson.IsPreview)
            return ContentAuthorization.Allowed;

        return await AuthorizeEnrollmentLessonAsync(subject.UserId, lesson.CourseId, lesson.Id);
    }

    private async Task<ContentAuthorization> AuthorizeResourceAsync(Lesson lesson, AccessSubject subject)
    {
        if (subject.IsAdmin)
            return ContentAuthorization.Allowed;

        var course = await _courseRepository.GetByIdAsync(lesson.CourseId);
        if (course == null || !string.Equals(course.Status, "published", StringComparison.OrdinalIgnoreCase))
            return ContentAuthorization.Denied;

        return await AuthorizeEnrollmentLessonAsync(subject.UserId, lesson.CourseId, lesson.Id);
    }

    private async Task<ContentAuthorization> AuthorizeEnrollmentLessonAsync(string? userId, string courseId, string lessonId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ContentAuthorization.Denied;

        var access = await _courseLessonAccessService.CanAccessAsync(userId, courseId, lessonId);
        return new ContentAuthorization(access.IsAllowed, access.IsLocked, access.DenialMessage);
    }

    private static void ThrowIfLocked(ContentAuthorization authorization)
    {
        if (authorization.IsLocked)
            throw new ForbiddenException(authorization.DenialMessage ?? "This lesson is locked. Complete the previous lesson to continue.");
    }

    private LessonContentAccessDto CreateAccessUrl(
        string lessonId,
        string? resourceId,
        string contentType,
        AccessSubject subject,
        string endpoint)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(_accessUrlLifetime);
        var token = CreateAccessToken(lessonId, resourceId, contentType, subject, expiresAt.UtcDateTime);
        return new LessonContentAccessDto
        {
            Url = $"{endpoint}?access={Uri.EscapeDataString(token)}",
            ExpiresAt = expiresAt
        };
    }

    private string CreateAccessToken(
        string lessonId,
        string? resourceId,
        string contentType,
        AccessSubject subject,
        DateTime expiresAt)
    {
        var claims = new List<Claim>
        {
            new(ContentTypeClaim, contentType),
            new(LessonIdClaim, lessonId),
            new(AdminClaim, subject.IsAdmin.ToString()),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        if (!string.IsNullOrWhiteSpace(resourceId))
            claims.Add(new Claim(ResourceIdClaim, resourceId));
        if (!string.IsNullOrWhiteSpace(subject.UserId))
            claims.Add(new Claim(UserIdClaim, subject.UserId));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: _signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<AccessTicket?> ValidateAccessTokenAsync(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        try
        {
            var validationResult = await new JsonWebTokenHandler()
                .ValidateTokenAsync(accessToken, _tokenValidationParameters);

            if (!validationResult.IsValid || validationResult.ClaimsIdentity == null)
                return null;

            var principal = new ClaimsPrincipal(validationResult.ClaimsIdentity);
            var lessonId = principal.FindFirstValue(LessonIdClaim);
            var contentType = principal.FindFirstValue(ContentTypeClaim);
            if (string.IsNullOrWhiteSpace(lessonId) || string.IsNullOrWhiteSpace(contentType))
                return null;

            var isAdmin = bool.TryParse(principal.FindFirstValue(AdminClaim), out var parsedAdmin) && parsedAdmin;
            return new AccessTicket(
                lessonId,
                principal.FindFirstValue(ResourceIdClaim),
                contentType,
                new AccessSubject(principal.FindFirstValue(UserIdClaim), isAdmin));
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private ProtectedLessonContent? ResolvePlatformFile(string storageUrl)
    {
        if (!IsPlatformLessonFile(storageUrl) || string.IsNullOrWhiteSpace(_environment.WebRootPath))
            return null;

        var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "lessons"));
        var relativePath = storageUrl[LessonUploadsPrefix.Length..]
            .Replace("/", Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            .Replace("\\", Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
        var physicalPath = Path.GetFullPath(Path.Combine(uploadsRoot, relativePath));
        var rootWithSeparator = uploadsRoot.EndsWith(Path.DirectorySeparatorChar)
            ? uploadsRoot
            : uploadsRoot + Path.DirectorySeparatorChar;

        if (!physicalPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) || !File.Exists(physicalPath))
            return null;

        if (!_contentTypeProvider.TryGetContentType(physicalPath, out var contentType))
            contentType = "application/octet-stream";

        return new ProtectedLessonContent(physicalPath, contentType);
    }

    private static AccessSubject GetSubject(ClaimsPrincipal principal) => new(
        principal.FindFirstValue(ClaimTypes.NameIdentifier),
        principal.IsInRole("admin"));

    private static bool IsPlatformLessonFile(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.StartsWith(LessonUploadsPrefix, StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeExternalUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private sealed record AccessSubject(string? UserId, bool IsAdmin);
    private sealed record AccessTicket(string LessonId, string? ResourceId, string ContentType, AccessSubject Subject);
    private sealed record ContentAuthorization(bool IsAllowed, bool IsLocked, string? DenialMessage)
    {
        public static ContentAuthorization Allowed { get; } = new(true, false, null);
        public static ContentAuthorization Denied { get; } = new(false, false, null);
    }
}
