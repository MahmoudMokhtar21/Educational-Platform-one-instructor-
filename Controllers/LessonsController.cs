using Educatinal_Platform.DTOs;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Educatinal_Platform.Controllers
{
    [ApiController]
    [Route("api")]
    public class LessonsController : ControllerBase
    {
        private readonly ILessonService _lessonService;
        private readonly ILessonContentAccessService _lessonContentAccessService;

        public LessonsController(
            ILessonService lessonService,
            ILessonContentAccessService lessonContentAccessService)
        {
            _lessonService = lessonService;
            _lessonContentAccessService = lessonContentAccessService;
        }

        // Course-management metadata includes storage references only for admins.
        // Students consume lesson content through the protected streaming endpoints below.
        [Authorize(Roles = "admin")]
        [HttpGet("courses/{courseId}/lessons")]
        public async Task<IActionResult> GetByCourse(
            string courseId)
        {
            var lessons =
                await _lessonService.GetByCourseIdAsync(courseId, includeStorageUrls: true);

            return Ok(lessons);
        }

        [Authorize(Roles = "admin")]
        [HttpGet("lessons/{id}")]
        public async Task<IActionResult> GetById(
            string id)
        {
            var lesson =
                await _lessonService.GetByIdAsync(id, includeStorageUrls: true);

            if (lesson == null)
                return NotFound(new
                {
                    message = "Lesson not found"
                });

            return Ok(lesson);
        }

        [AllowAnonymous]
        [HttpGet("lessons/{lessonId}/video-access")]
        public async Task<IActionResult> GetVideoAccess(string lessonId)
        {
            var access = await _lessonContentAccessService.GetVideoAccessAsync(User, lessonId);
            return access == null ? NotFound() : Ok(access);
        }

        [AllowAnonymous]
        [HttpGet("lessons/{lessonId}/video")]
        public async Task<IActionResult> StreamVideo(string lessonId, [FromQuery] string? access)
        {
            var content = await _lessonContentAccessService.OpenVideoAsync(lessonId, access);
            if (content == null)
                return NotFound();

            ApplyProtectedContentHeaders();
            return PhysicalFile(content.PhysicalPath, content.ContentType, enableRangeProcessing: true);
        }

        [AllowAnonymous]
        [HttpGet("lessons/{lessonId}/resources/{resourceId}/access")]
        public async Task<IActionResult> GetResourceAccess(string lessonId, string resourceId)
        {
            var access = await _lessonContentAccessService.GetResourceAccessAsync(User, lessonId, resourceId);
            return access == null ? NotFound() : Ok(access);
        }

        [AllowAnonymous]
        [HttpGet("lessons/{lessonId}/resources/{resourceId}")]
        public async Task<IActionResult> StreamResource(string lessonId, string resourceId, [FromQuery] string? access)
        {
            var content = await _lessonContentAccessService.OpenResourceAsync(lessonId, resourceId, access);
            if (content == null)
                return NotFound();

            ApplyProtectedContentHeaders();
            return PhysicalFile(content.PhysicalPath, content.ContentType, enableRangeProcessing: true);
        }

        private void ApplyProtectedContentHeaders()
        {
            Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                Private = true,
                NoStore = true,
                MaxAge = TimeSpan.Zero
            };
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
        }

        // 3. Create lesson - Admin only
        [Authorize(Roles = "admin")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        [HttpPost("courses/{courseId}/lessons")]
        public async Task<IActionResult> Create(
            string courseId,
            [FromForm] CreateLessonDto dto)
        {
            var lesson =
                await _lessonService.CreateAsync(
                    courseId,
                    dto);

            return Ok(lesson);
        }

        [Authorize(Roles = "admin")]
        [HttpPut("courses/{courseId}/lessons/reorder")]
        public async Task<IActionResult> Reorder(string courseId, [FromBody] ReorderLessonsDto dto)
        {
            await _lessonService.ReorderAsync(courseId, dto);
            return Ok(new { message = "Lessons reordered successfully." });
        }

        [Authorize(Roles = "admin")]
        [HttpPut("courses/{courseId}/lessons/{lessonId}/track")]
        public async Task<IActionResult> MoveToTrack(string courseId, string lessonId, [FromBody] MoveLessonToTrackDto dto)
        {
            await _lessonService.MoveToTrackAsync(courseId, lessonId, dto);
            return NoContent();
        }

        // 4. Update lesson - Admin only
        [Authorize(Roles = "admin")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        [HttpPut("lessons/{id}")]
        public async Task<IActionResult> Update(
            string id,
            [FromForm] UpdateLessonDto dto)
        {
            await _lessonService.UpdateAsync(
                id,
                dto);

            return NoContent();
        }
        [Authorize(Roles = "admin")]
        [HttpPut("{lessonId}/resources/{resourceId}")]
        public async Task<IActionResult> UpdateResource(
            string lessonId,
            string resourceId,
            [FromForm] UpdateLessonResourceDto dto)
        {
            await _lessonService.UpdateResourceAsync(
                lessonId,
                resourceId,
                dto);

            return NoContent();
        }

        // 5. Delete lesson - Admin only
        [Authorize(Roles = "admin")]
        [HttpDelete("lessons/{id}")]
        public async Task<IActionResult> Delete(
            string id)
        {
            await _lessonService.DeleteAsync(id);

            return NoContent();
        }


        [Authorize(Roles = "admin")]
        [HttpDelete("{lessonId}/resources/{resourceId}")]
        public async Task<IActionResult> DeleteResource(
            string lessonId,
            string resourceId)
        {
            await _lessonService.DeleteResourceAsync(
                lessonId,
                resourceId);

            return NoContent();
        }
    }
}
