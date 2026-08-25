using AutoMapper;
using Educatinal_Platform.DTOs;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;

namespace Educatinal_Platform.Services
{
    public interface ICategoryService
    {
        Task<List<CategoryResponseDto>> GetAllAsync();

        Task<CategoryResponseDto?> GetByIdAsync(string id);

        Task<CategoryResponseDto> CreateAsync(CreateCategoryDto dto);

        Task UpdateAsync(string id, UpdateCategoryDto dto);

        Task DeleteAsync(string id);
    }
    public class CategoryService : ICategoryService
    {

        private readonly ICategoryRepository _categoryRepo;
        private readonly IMapper _mapper;

        public CategoryService(
            ICategoryRepository categoryRepo,
            IMapper mapper)
        {
            _categoryRepo = categoryRepo;
            _mapper = mapper;
        }

        public async Task<List<CategoryResponseDto>> GetAllAsync()
        {
            var categories = await _categoryRepo.GetAllAsync();

            return _mapper.Map<List<CategoryResponseDto>>(categories);
        }

        public async Task<CategoryResponseDto?> GetByIdAsync(string id)
        {
            var category = await _categoryRepo.GetByIdAsync(id);

            if (category == null)
                return null;

            return _mapper.Map<CategoryResponseDto>(category);
        }

        public async Task<CategoryResponseDto> CreateAsync(
            CreateCategoryDto dto)
        {
            var name = dto.Name.Trim();

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Category name is required.");

            if (await _categoryRepo.ExistsByNameAsync(name))
                throw new ArgumentException(
                    "A category with this name already exists.");

            var category = new Category
            {
                Name = name,
                Slug = GenerateSlug(name),
                Description = dto.Description,
                ImageUrl = dto.ImageUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (await _categoryRepo.ExistsBySlugAsync(category.Slug))
            {
                category.Slug =
                    $"{category.Slug}-{DateTime.UtcNow.Ticks}";
            }

            var createdCategory =
                await _categoryRepo.CreateAsync(category);

            return _mapper.Map<CategoryResponseDto>(
                createdCategory);
        }

        public async Task UpdateAsync(
            string id,
            UpdateCategoryDto dto)
        {
            var category =
                await _categoryRepo.GetByIdAsync(id);

            if (category == null)
                throw new KeyNotFoundException(
                    "Category not found.");

            if (dto.Name != null)
            {
                var name = dto.Name.Trim();

                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException(
                        "Category name cannot be empty.");

                if (!name.Equals(
                        category.Name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (await _categoryRepo.ExistsByNameAsync(name))
                        throw new ArgumentException(
                            "A category with this name already exists.");

                    category.Name = name;
                    category.Slug = GenerateSlug(name);

                    if (await _categoryRepo.ExistsBySlugAsync(
                            category.Slug))
                    {
                        category.Slug =
                            $"{category.Slug}-{DateTime.UtcNow.Ticks}";
                    }
                }
            }

            if (dto.Description != null)
                category.Description = dto.Description;

            if (dto.ImageUrl != null)
                category.ImageUrl = dto.ImageUrl;

            if (dto.IsActive.HasValue)
                category.IsActive = dto.IsActive.Value;

            category.UpdatedAt = DateTime.UtcNow;

            await _categoryRepo.UpdateAsync(
                id,
                category);
        }

        public async Task DeleteAsync(string id)
        {
            var category =
                await _categoryRepo.GetByIdAsync(id);

            if (category == null)
                throw new KeyNotFoundException(
                    "Category not found.");

            await _categoryRepo.DeleteAsync(id);
        }

        private string GenerateSlug(string name)
        {
            return name
                .ToLowerInvariant()
                .Trim()
                .Replace(" ", "-")
                .Replace(".", "")
                .Replace("'", "")
                .Replace("\"", "");
        }
    }
}
