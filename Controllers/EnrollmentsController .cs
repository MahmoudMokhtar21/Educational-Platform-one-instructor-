using Educatinal_Platform.DTOs;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Educatinal_Platform.Controllers;

[ApiController]
[Route("api/enrollments")]
[Authorize]
public class EnrollmentsController : ControllerBase
{
    private readonly IEnrollmentService _enrollmentService;

    public EnrollmentsController(IEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    // Creates either a free enrollment or a server-owned paid checkout attempt.
    // The optional phone is used only by the real Paymob provider; the server
    // remains authoritative for course, price, currency, and student identity.
    [HttpPost("courses/{courseId}")]
    public async Task<IActionResult> Enroll(
        string courseId,
        [FromBody] EnrollmentPaymentRequest? request,
        CancellationToken cancellationToken)
    {
        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(new { message = "User ID not found in token." });
        }

        var customer = new PaymentCustomer
        {
            Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            FirstName = User.FindFirst("FirstName")?.Value ?? string.Empty,
            LastName = User.FindFirst("LastName")?.Value ?? string.Empty,
            PhoneNumber = request?.PhoneNumber
        };

        var result = await _enrollmentService.EnrollAsync(
            studentId,
            courseId,
            customer,
            request?.AccessType,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new
            {
                code = result.ErrorCode,
                message = result.Message
            });
        }

        if (result.RequiresPayment)
        {
            return Ok(new
            {
                requiresPayment = true,
                provider = result.Provider,
                paymentReference = result.PaymentReference,
                checkoutUrl = result.CheckoutUrl,
                isMock = result.IsMock,
                accessType = result.AccessType,
                message = result.Message
            });
        }

        return Ok(new
        {
            success = true,
            enrollmentId = result.EnrollmentId,
            accessType = result.AccessType,
            message = result.Message
        });
    }

    [HttpGet("courses/{courseId}")]
    public async Task<IActionResult> GetMyEnrollment(string courseId)
    {
        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId)) return Unauthorized();

        var enrollment = await _enrollmentService.GetMyEnrollmentAsync(studentId, courseId);
        if (enrollment == null)
        {
            return NotFound(new { message = "Enrollment not found." });
        }

        return Ok(enrollment);
    }

    [HttpPost("courses/{courseId}/lessons/{lessonId}/progress")]
    public async Task<IActionResult> UpdateProgress(
        string courseId,
        string lessonId,
        [FromBody] UpdateLessonProgressRequest request)
    {
        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId)) return Unauthorized();

        return Ok(await _enrollmentService.UpdateLessonProgressAsync(
            studentId,
            courseId,
            lessonId,
            request.WatchDurationSeconds));
    }

    [HttpGet("my-courses")]
    public async Task<IActionResult> GetMyCourses()
    {
        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId)) return Unauthorized();

        return Ok(await _enrollmentService.GetMyCoursesAsync(studentId));
    }

    [HttpDelete("courses/{courseId}")]
    public async Task<IActionResult> CancelEnrollment(string courseId)
    {
        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId)) return Unauthorized();

        await _enrollmentService.CancelEnrollmentAsync(studentId, courseId);
        return Ok(new
        {
            success = true,
            message = "Enrollment cancelled successfully."
        });
    }
}

public class UpdateLessonProgressRequest
{
    public int WatchDurationSeconds { get; set; }

    // Retained only so old clients do not fail model binding. Completion is now
    // calculated from the server-validated progress and this value is ignored.
    public bool IsCompleted { get; set; }
}
