using Educatinal_Platform.DTOs;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Educatinal_Platform.Controllers
{
    [ApiController]
    [Authorize(Roles = "admin")]
    [Route("api/courses/{courseId}/tracks")]
    public class TracksController : ControllerBase
    {
        private readonly ITrackService _trackService;
        private readonly ILessonService _lessonService;

        public TracksController(ITrackService trackService, ILessonService lessonService)
        {
            _trackService = trackService;
            _lessonService = lessonService;
        }

        [HttpGet]
        public async Task<IActionResult> GetByCourse(string courseId)
        {
            var tracks = await _trackService.GetByCourseIdAsync(courseId);
            var lessons = await _lessonService.GetByCourseIdAsync(courseId, includeStorageUrls: true);
            return Ok(tracks.Select(track => ToDto(track, lessons)));
        }

        [HttpPost]
        public async Task<IActionResult> Create(string courseId, [FromBody] CreateTrackDto dto)
        {
            var track = await _trackService.CreateAsync(courseId, dto);
            return CreatedAtAction(nameof(GetByCourse), new { courseId }, ToDto(track, Array.Empty<LessonDto>()));
        }

        [HttpPut("{trackId}")]
        public async Task<IActionResult> Update(string courseId, string trackId, [FromBody] UpdateTrackDto dto)
        {
            await _trackService.UpdateAsync(courseId, trackId, dto);
            return NoContent();
        }

        [HttpDelete("{trackId}")]
        public async Task<IActionResult> Delete(string courseId, string trackId, [FromQuery] string? moveLessonsToTrackId)
        {
            await _trackService.DeleteAsync(courseId, trackId, moveLessonsToTrackId);
            return NoContent();
        }

        [HttpPut("reorder")]
        public async Task<IActionResult> Reorder(string courseId, [FromBody] ReorderTracksDto dto)
        {
            await _trackService.ReorderAsync(courseId, dto);
            return Ok(new { message = "Tracks reordered successfully." });
        }

        [HttpPut("{trackId}/lessons/reorder")]
        public async Task<IActionResult> ReorderLessons(string courseId, string trackId, [FromBody] ReorderLessonsDto dto)
        {
            await _lessonService.ReorderInTrackAsync(courseId, trackId, dto);
            return Ok(new { message = "Lessons reordered successfully." });
        }

        private static TrackDto ToDto(Models.Track track, IEnumerable<LessonDto> lessons) => new()
        {
            Id = track.Id,
            CourseId = track.CourseId,
            Title = track.Title,
            Description = track.Description,
            OrderIndex = track.OrderIndex,
            CreatedAt = track.CreatedAt,
            Lessons = lessons
                .Where(lesson => string.Equals(lesson.TrackId, track.Id, StringComparison.Ordinal))
                .OrderBy(lesson => lesson.OrderIndex)
                .ToList()
        };
    }
}
