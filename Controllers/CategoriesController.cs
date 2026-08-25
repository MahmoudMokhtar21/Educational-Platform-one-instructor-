using Educatinal_Platform.DTOs;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Educatinal_Platform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        // 1. Get all categories
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _categoryService.GetAllAsync();

            return Ok(categories);
        }

        // 2. Get category by ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var category = await _categoryService.GetByIdAsync(id);

            if (category == null)
                return NotFound(new
                {
                    message = "Category not found"
                });

            return Ok(category);
        }

        // 3. Create category - Admin only
        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateCategoryDto dto)
        {
            var category = await _categoryService.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = category.Id },
                category);
        }

        // 4. Update category - Admin only
        [Authorize(Roles = "admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            string id,
            [FromBody] UpdateCategoryDto dto)
        {
            await _categoryService.UpdateAsync(id, dto);

            return NoContent();
        }

        // 5. Delete category - Admin only
        [Authorize(Roles = "admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _categoryService.DeleteAsync(id);

            return NoContent();
        }
    }
}