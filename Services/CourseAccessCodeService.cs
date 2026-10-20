using System.Security.Cryptography;
using System.Text;
using Educatinal_Platform.DTOs;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using EduPlatformAPI.Repositories;
using MongoDB.Driver;

namespace Educatinal_Platform.Services;

public interface ICourseAccessCodeService
{
    Task<GeneratedCourseAccessCodeDto?> GenerateAsync(string courseId, CancellationToken cancellationToken = default);
    Task<List<CourseAccessCodeListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string id, CancellationToken cancellationToken = default);
    Task<CourseAccessCodeRedemptionResult> RedeemAsync(string studentId, string courseId, string submittedCode, CancellationToken cancellationToken = default);
}

public sealed class CourseAccessCodeService : ICourseAccessCodeService
{
    private static readonly TimeSpan Validity = TimeSpan.FromMinutes(30);
    private readonly ICourseAccessCodeRepository _codes;
    private readonly ICourseRepository _courses;
    private readonly IEnrollmentRepository _enrollments;
    private readonly IEnrollmentService _enrollmentService;
    private readonly ILogger<CourseAccessCodeService> _logger;

    public CourseAccessCodeService(ICourseAccessCodeRepository codes, ICourseRepository courses, IEnrollmentRepository enrollments, IEnrollmentService enrollmentService, ILogger<CourseAccessCodeService> logger)
    {
        _codes = codes;
        _courses = courses;
        _enrollments = enrollments;
        _enrollmentService = enrollmentService;
        _logger = logger;
    }

    public async Task<GeneratedCourseAccessCodeDto?> GenerateAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var course = await _courses.GetByIdAsync(courseId);
        if (course is null) return null;
        var now = DateTime.UtcNow;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var raw = CreateRawCode();
            var code = new CourseAccessCode
            {
                CourseId = course.Id,
                CodeHash = Hash(raw),
                CodeHint = Mask(raw),
                CreatedAt = now,
                ExpiresAt = now.Add(Validity)
            };
            try
            {
                await _codes.CreateAsync(code, cancellationToken);
                return new GeneratedCourseAccessCodeDto { Id = code.Id, CourseId = code.CourseId, CourseTitle = course.Title, Code = raw, MaskedCode = code.CodeHint, CreatedAt = code.CreatedAt, ExpiresAt = code.ExpiresAt, Status = "active" };
            }
            catch (MongoWriteException) when (attempt < 2)
            {
                // A hash collision is cryptographically implausible, but the unique
                // index remains the final authority and retry is harmless.
            }
        }
        throw new InvalidOperationException("A secure access code could not be generated.");
    }

    public async Task<List<CourseAccessCodeListItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var codes = await _codes.GetAllAsync(cancellationToken);
        var courses = await _courses.GetAllAsync(1, Math.Max(1000, codes.Count));
        var titles = courses.ToDictionary(item => item.Id, item => item.Title, StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        return codes.Select(code => new CourseAccessCodeListItemDto
        {
            Id = code.Id, CourseId = code.CourseId, CourseTitle = titles.GetValueOrDefault(code.CourseId, "Deleted course"), MaskedCode = code.CodeHint,
            CreatedAt = code.CreatedAt, ExpiresAt = code.ExpiresAt, RedeemedAt = code.RedeemedAt, RedeemedByStudentId = code.RedeemedByStudentId,
            Status = StatusOf(code, now), CanRevoke = IsActive(code, now) && string.IsNullOrWhiteSpace(code.RedemptionClaimToken)
        }).ToList();
    }

    public Task<bool> RevokeAsync(string id, CancellationToken cancellationToken = default) => _codes.RevokeAsync(id, DateTime.UtcNow, cancellationToken);

    public async Task<CourseAccessCodeRedemptionResult> RedeemAsync(string studentId, string courseId, string submittedCode, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(submittedCode);
        if (string.IsNullOrWhiteSpace(normalized)) return Fail("access_code_invalid", "Enter a valid course access code.");
        var hash = Hash(normalized);
        var known = await _codes.GetByHashAsync(hash, cancellationToken);
        if (known is null) return Fail("access_code_invalid", "This course access code is invalid.");
        if (!string.Equals(known.CourseId, courseId, StringComparison.Ordinal)) return Fail("access_code_wrong_course", "This access code is for a different course.");
        var now = DateTime.UtcNow;
        if (!IsActive(known, now)) return Fail("access_code_" + StatusOf(known, now), "This course access code is no longer available.");

        var claimToken = Guid.NewGuid().ToString("N");
        var claimed = await _codes.TryClaimAsync(hash, courseId, studentId, claimToken, now, cancellationToken);
        if (claimed is null)
        {
            var current = await _codes.GetByHashAsync(hash, cancellationToken);
            return Fail("access_code_unavailable", current is null ? "This course access code is invalid." : "This course access code is no longer available.");
        }

        try
        {
            // If an earlier request created the enrollment but stopped before the
            // final update, finish that redemption instead of granting the code to
            // another student after the claim lease expires.
            var recoveredEnrollment = await _enrollments.GetByCourseAccessCodeIdAsync(claimed.Id);
            if (recoveredEnrollment is not null)
            {
                var finalized = await _codes.FinalizeRedemptionAsync(claimed.Id, claimToken, recoveredEnrollment.StudentId, DateTime.UtcNow, cancellationToken);
                if (string.Equals(recoveredEnrollment.StudentId, studentId, StringComparison.Ordinal))
                    return finalized ? Success(recoveredEnrollment.Id, "Access code redeemed. Sequential course access is active.") : Fail("access_code_finalizing", "Your enrollment is active; please refresh the course page.");
                return Fail("access_code_redeemed", "This course access code has already been used.");
            }

            var enrollment = await _enrollmentService.GrantSequentialAccessCodeEnrollmentAsync(studentId, courseId, claimed.Id, cancellationToken);
            if (!enrollment.Success)
            {
                await _codes.ReleaseClaimAsync(claimed.Id, claimToken, cancellationToken);
                return Fail(enrollment.ErrorCode ?? "enrollment_failed", enrollment.Message);
            }
            if (!await _codes.FinalizeRedemptionAsync(claimed.Id, claimToken, studentId, DateTime.UtcNow, cancellationToken))
            {
                _logger.LogError("Enrollment {EnrollmentId} was created for course access code {CodeId}, but the code could not be finalized immediately.", enrollment.EnrollmentId, claimed.Id);
                await _codes.ReleaseClaimAsync(claimed.Id, claimToken, cancellationToken);
                return Fail("access_code_finalizing", "Your enrollment is active; please refresh the course page.");
            }
            return Success(enrollment.EnrollmentId!, enrollment.Message);
        }
        catch
        {
            await _codes.ReleaseClaimAsync(claimed.Id, claimToken, cancellationToken);
            throw;
        }
    }

    private static CourseAccessCodeRedemptionResult Success(string enrollmentId, string message) => new() { Success = true, EnrollmentId = enrollmentId, AccessType = CourseAccessTypes.Sequential, Message = message };
    private static CourseAccessCodeRedemptionResult Fail(string code, string message) => new() { Success = false, ErrorCode = code, AccessType = CourseAccessTypes.Sequential, Message = message };
    private static bool IsActive(CourseAccessCode code, DateTime now) => code.RedeemedAt is null && code.RevokedAt is null && code.ExpiresAt > now;
    private static string StatusOf(CourseAccessCode code, DateTime now) => code.RedeemedAt is not null ? "redeemed" : code.RevokedAt is not null ? "revoked" : code.ExpiresAt <= now ? "expired" : "active";
    private static string CreateRawCode() => "EDU-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
    private static string Normalize(string value) => value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(value))));
    private static string Mask(string value) => value.Length <= 8 ? "••••" : value[..7] + "••••" + value[^4..];
}