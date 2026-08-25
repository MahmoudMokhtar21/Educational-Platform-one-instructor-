using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace Educatinal_Platform.Controllers
{
    [ApiController]
    [Route("api/enrollments")]
    [Authorize]
    public class EnrollmentsController : ControllerBase
    {
        private readonly IEnrollmentService _enrollmentService;

        public EnrollmentsController(
            IEnrollmentService enrollmentService)
        {
            _enrollmentService = enrollmentService;
        }

        // 1. Enroll in a course
        [HttpPost("courses/{courseId}")]
        public async Task<IActionResult> Enroll(
            string courseId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(studentId))
                return Unauthorized(new
                {
                    message = "User ID not found in token."
                });

            var result =
                await _enrollmentService.EnrollAsync(
                    studentId,
                    courseId);

            if (!result.Success)
                return BadRequest(new
                {
                    message = result.Message
                });

            if (result.RequiresPayment)
            {
                return Ok(new
                {
                    requiresPayment = true,
                    paymentIntentId =
                        result.PaymentIntentId,
                    clientSecret =
                        result.ClientSecret,
                    message = result.Message
                });
            }

            return Ok(new
            {
                success = true,
                enrollmentId =
                    result.EnrollmentId,
                message = result.Message
            });
        }

        // 2. Get my enrollment
        [HttpGet("courses/{courseId}")]
        public async Task<IActionResult> GetMyEnrollment(
            string courseId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(studentId))
                return Unauthorized();

            var enrollment =
                await _enrollmentService
                    .GetMyEnrollmentAsync(
                        studentId,
                        courseId);

            if (enrollment == null)
                return NotFound(new
                {
                    message = "Enrollment not found."
                });

            return Ok(enrollment);
        }

        // 3. Update lesson progress
        [HttpPost(
            "courses/{courseId}/lessons/{lessonId}/progress")]
        public async Task<IActionResult> UpdateProgress(
            string courseId,
            string lessonId,
            [FromBody] UpdateLessonProgressRequest request)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(studentId))
                return Unauthorized();

            await _enrollmentService
                .UpdateLessonProgressAsync(
                    studentId,
                    courseId,
                    lessonId,
                    request.WatchDurationSeconds,
                    request.IsCompleted);

            return Ok(new
            {
                success = true,
                message = "Lesson progress updated successfully."
            });
        }

        [HttpGet("my-courses")]
        public async Task<IActionResult> GetMyCourses()
        {
            var studentId =
                User.FindFirst(ClaimTypes.NameIdentifier) ?.Value;
                    

            if (string.IsNullOrEmpty(studentId))
                return Unauthorized();

            var result =
                await _enrollmentService
                    .GetMyCoursesAsync(studentId);

            return Ok(result);
        }

        [HttpDelete("courses/{courseId}")]
        public async Task<IActionResult> CancelEnrollment(
            string courseId)
        {
            var studentId =
                User.FindFirst(ClaimTypes.NameIdentifier) ?.Value;
               
                   
            if (string.IsNullOrEmpty(studentId))
                return Unauthorized();

            await _enrollmentService
                .CancelEnrollmentAsync(
                    studentId,
                    courseId);

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

        public bool IsCompleted { get; set; }
    }
}