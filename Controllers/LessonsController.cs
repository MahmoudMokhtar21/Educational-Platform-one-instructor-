using Educatinal_Platform.DTOs;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Educatinal_Platform.Controllers
{
    [ApiController]
    [Route("api")]
    public class LessonsController : ControllerBase
    {
        private readonly ILessonService _lessonService;

        public LessonsController(ILessonService lessonService)
        {
            _lessonService = lessonService;
        }

        // 1. Get all lessons of a course
        [HttpGet("courses/{courseId}/lessons")]
        public async Task<IActionResult> GetByCourse(
            string courseId)
        {
            var lessons =
                await _lessonService.GetByCourseIdAsync(courseId);

            return Ok(lessons);
        }

        // 2. Get lesson by ID
        [HttpGet("lessons/{id}")]
        public async Task<IActionResult> GetById(
            string id)
        {
            var lesson =
                await _lessonService.GetByIdAsync(id);

            if (lesson == null)
                return NotFound(new
                {
                    message = "Lesson not found"
                });

            return Ok(lesson);
        }

        // 3. Create lesson - Admin only
        [Authorize(Roles = "admin")]
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

        // 4. Update lesson - Admin only
        [Authorize(Roles = "admin")]
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