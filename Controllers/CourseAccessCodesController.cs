using System.Security.Claims;
using Educatinal_Platform.DTOs;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Educatinal_Platform.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class CourseAccessCodesController : ControllerBase
{
    private readonly ICourseAccessCodeService _service;
    public CourseAccessCodesController(ICourseAccessCodeService service) => _service = service;

    [HttpPost("admin/course-access-codes")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Generate([FromBody] GenerateCourseAccessCodeRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.CourseId)) return BadRequest(new { message = "A course is required." });
        var result = await _service.GenerateAsync(request.CourseId, cancellationToken);
        return result is null ? NotFound(new { message = "Course not found." }) : Ok(result);
    }

    [HttpGet("admin/course-access-codes")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await _service.GetAllAsync(cancellationToken));

    [HttpPost("admin/course-access-codes/{id}/revoke")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Revoke(string id, CancellationToken cancellationToken)
    {
        var revoked = await _service.RevokeAsync(id, cancellationToken);
        return revoked ? Ok(new { success = true, message = "Access code revoked." }) : BadRequest(new { message = "Only an unused, active access code can be revoked." });
    }

    [HttpPost("enrollments/courses/{courseId}/redeem-code")]
    public async Task<IActionResult> Redeem(string courseId, [FromBody] RedeemCourseAccessCodeRequest? request, CancellationToken cancellationToken)
    {
        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId)) return Unauthorized(new { message = "User ID not found in token." });
        var result = await _service.RedeemAsync(studentId, courseId, request?.Code ?? string.Empty, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}