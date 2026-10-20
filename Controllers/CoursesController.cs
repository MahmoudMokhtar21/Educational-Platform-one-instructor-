using Educatinal_Platform.DTOs;
using Educatinal_Platform.Models;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Educatinal_Platform.Controllers
{
    
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CoursesController : ControllerBase
    {
        private readonly ICourseService _courseService;

        public CoursesController(ICourseService courseService)
        {
            _courseService = courseService;
        }

       
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetCourses(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] string? level = null,
            [FromQuery] string? sort = null)
        {
            var result = await _courseService.GetPublishedCoursesAsync(page, limit, search, category, level, sort);
            return Ok(result);
        }
       
       
       
        [AllowAnonymous]
        [HttpGet("{slug}")]
        public async Task<IActionResult> GetCourse(string slug)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var isAdmin = User.IsInRole("admin");
            var course = await _courseService.GetCourseDetailAsync(slug, studentId, isAdmin);

            if (course == null)
                return NotFound(new
                {
                    message = "Course not found"
                });

            return Ok(course);
        }

      
        [Authorize(Roles = "admin")]
        [HttpGet("admin")]
        public async Task<IActionResult> GetAdminCourses(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] string? level = null,
            [FromQuery] string? sort = null)
        {
            return Ok(await _courseService.GetAdminCoursesAsync(page, limit, search, category, level, sort));
        }

        [Authorize(Roles = "admin")]
        [HttpGet("admin/{id}")]
        public async Task<IActionResult> GetAdminCourse(string id)
        {
            var course = await _courseService.GetAdminCourseAsync(id);
            return course == null ? NotFound(new { message = "Course not found" }) : Ok(course);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> CreateCourse([FromForm] CreateCourseDto dto)
        {
            var course = await _courseService.CreateCourseAsync(dto);
            return CreatedAtAction(nameof(GetCourse), new { slug = course.Slug }, course);
        }

        [Authorize(Roles = "admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCourse(string id, [FromForm] UpdateCourseDto dto)
        {
            await _courseService.UpdateCourseAsync(id, dto);
            return NoContent();
        }

        
        [Authorize(Roles = "admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(string id)
        {
            await _courseService.DeleteCourseAsync(id);
            return NoContent();
        }

       
        [Authorize(Roles = "admin")]
        [HttpPost("{id}/publish")]
        public async Task<IActionResult> PublishCourse(string id)
        {
            await _courseService.PublishCourseAsync(id);
            return Ok(new { message = "Course published successfully" });
        }

       
      
    }
}
