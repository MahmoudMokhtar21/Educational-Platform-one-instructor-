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
        public async Task<IActionResult> GetCourses([FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            var result = await _courseService.GetPublishedCoursesAsync(page, limit);
            return Ok(result);
        }
       
       
       
        [AllowAnonymous]
        [HttpGet("{slug}")]
        public async Task<IActionResult> GetCourse(string slug)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var course = await _courseService.GetCourseDetailAsync(slug, studentId);

            if (course == null)
                return NotFound(new
                {
                    message = "Course not found"
                });

            return Ok(course);
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
