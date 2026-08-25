namespace Educatinal_Platform.Controllers
{
    using global::Educatinal_Platform.DTOs;
    using global::Educatinal_Platform.Services;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;

    namespace Educatinal_Platform.Controllers
    {
        [ApiController]
        [Route("api")]
        [Authorize]
        public class ReviewsController : ControllerBase
        {
            private readonly IReviewService _reviewService;

            public ReviewsController(
                IReviewService reviewService)
            {
                _reviewService = reviewService;
            }

            // 1. Get all reviews for a course
            [HttpGet("courses/{courseId}/reviews")]
            public async Task<IActionResult> GetCourseReviews(
                string courseId)
            {
                var reviews =
                    await _reviewService
                        .GetByCourseIdAsync(courseId);

                return Ok(reviews);
            }

            // 2. Get review by ID
            [HttpGet("reviews/{id}")]
            public async Task<IActionResult> GetReview(
                string id)
            {
                var review =
                    await _reviewService.GetByIdAsync(id);

                if (review == null)
                    return NotFound(new
                    {
                        message = "Review not found."
                    });

                return Ok(review);
            }

            // 3. Create review
            [HttpPost("courses/{courseId}/reviews")]
            public async Task<IActionResult> CreateReview(
                string courseId,
                [FromBody] CreateReviewDto dto)
            {
                var studentId =
                    User.FindFirst(
                        ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(studentId))
                    return Unauthorized(new
                    {
                        message = "User ID not found in token."
                    });

                var review =
                    await _reviewService.CreateAsync(
                        studentId,
                        courseId,
                        dto);

                return Ok(review);
            }

            // 4. Update my review
            [HttpPut("reviews/{id}")]
            public async Task<IActionResult> UpdateReview(
                string id,
                [FromBody] UpdateReviewDto dto)
            {
                var studentId =
                    User.FindFirst(
                        ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(studentId))
                    return Unauthorized();

                await _reviewService.UpdateAsync(
                    id,
                    studentId,
                    dto);

                return NoContent();
            }

            // 5. Delete my review
            [HttpDelete("reviews/{id}")]
            public async Task<IActionResult> DeleteReview(
                string id)
            {
                var studentId =
                    User.FindFirst(
                        ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(studentId))
                    return Unauthorized();

                await _reviewService.DeleteAsync(
                    id,
                    studentId);

                return NoContent();
            }
        }
    }
}
