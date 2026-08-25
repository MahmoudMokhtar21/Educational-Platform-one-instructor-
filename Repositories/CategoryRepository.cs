using Educatinal_Platform.Models;
using MongoDB.Driver;

namespace Educatinal_Platform.Repositories
{
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllAsync();

        Task<Category?> GetByIdAsync(string id);

        Task<Category?> GetBySlugAsync(string slug);

        Task<Category> CreateAsync(Category category);

        Task UpdateAsync(string id, Category category);

        Task DeleteAsync(string id);

        Task<bool> ExistsByNameAsync(string name);

        Task<bool> ExistsBySlugAsync(string slug);
    }
    public class CategoryRepository : ICategoryRepository
    {
        private readonly IMongoCollection<Category> _categories;

        public CategoryRepository(IMongoDatabase database)
        {
            _categories = database.GetCollection<Category>("Categories");
        }

        public async Task<List<Category>> GetAllAsync()
        {
            return await _categories
                .Find(_ => true)
                .SortBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(string id)
        {
            return await _categories
                .Find(c => c.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<Category?> GetBySlugAsync(string slug)
        {
            return await _categories
                .Find(c => c.Slug == slug)
                .FirstOrDefaultAsync();
        }

        public async Task<Category> CreateAsync(Category category)
        {
            await _categories.InsertOneAsync(category);
            return category;
        }

        public async Task UpdateAsync(string id, Category category)
        {
            await _categories.ReplaceOneAsync(
                c => c.Id == id,
                category);
        }

        public async Task DeleteAsync(string id)
        {
            await _categories.DeleteOneAsync(
                c => c.Id == id);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _categories
                .Find(c => c.Name.ToLower() == name.ToLower())
                .AnyAsync();
        }

        public async Task<bool> ExistsBySlugAsync(string slug)
        {
            return await _categories
                .Find(c => c.Slug == slug)
                .AnyAsync();
        }
    }
}
