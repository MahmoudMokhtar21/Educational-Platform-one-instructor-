using Educatinal_Platform.Models;
using MongoDB.Driver;
using System.Reflection.Emit;

namespace Educatinal_Platform.Repositories
{
    public interface ICourseRepository
    {
        Task<List<Course>> GetAllAsync(int page, int limit, string? search = null, string? category = null, string? level = null, string? sort = null);
        Task<Course?> GetByIdAsync(string id);
        Task<Course?> GetBySlugAsync(string slug);
        Task<List<Course>> GetPublishedAsync(int page, int limit, string? search = null, string? category = null, string? level = null, string? sort = null);
        Task<Course> CreateAsync(Course course);
        Task UpdateAsync(string id, Course course);
        Task DeleteAsync(string id);
        Task<long> CountAsync(string? search = null, string? category = null, string? level = null);
        Task<long> CountPublishedAsync(string? search = null, string? category = null, string? level = null);
        Task<long> CountByStatusAsync(string status);
        Task<bool> ExistsAsync(string slug);
        Task UpdateStatsAsync(string courseId, int studentDelta, int ratingDelta);
        //Task<long> CountAsync( string? search = null,string? category = null,string? level = null);
        //Task<long> CountPublishedAsync(string? search = null, string? category = null, string? level = null);
    }
    public class CourseRepository : ICourseRepository
    {
        private readonly IMongoCollection<Course> _courses;

        public CourseRepository(IMongoDatabase database)
        {
           
            _courses = database.GetCollection<Course>("Courses");
        }

        public async Task<List<Course>> GetAllAsync(
            int page,
            int limit,
            string? search = null,
            string? category = null,
            string? level = null,
            string? sort = null)
        {
            var filter = BuildFilter(search, category, level);

            return await _courses
                .Find(filter)
                .Skip((page - 1) * limit)
                .Limit(limit)
                .Sort(GetSort(sort))
                .ToListAsync();
        }

        public async Task<Course?> GetByIdAsync(string id)
        {
            return await _courses.Find(c => c.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Course?> GetBySlugAsync(string slug)
        {
            return await _courses.Find(c => c.Slug == slug).FirstOrDefaultAsync();
        }

        public async Task<List<Course>> GetPublishedAsync(
            int page,
            int limit,
            string? search = null,
            string? category = null,
            string? level = null,
            string? sort = null)
        {
            var filter = Builders<Course>.Filter.And(
                Builders<Course>.Filter.Eq(c => c.Status, "published"),
                BuildFilter(search, category, level));

            return await _courses
                .Find(filter)
                .Skip((page - 1) * limit)
                .Limit(limit)
                .Sort(GetSort(sort))
                .ToListAsync();
        }

        public async Task<Course> CreateAsync(Course course)
        {
            await _courses.InsertOneAsync(course);
            return course;
        }

        public async Task UpdateAsync(string id, Course course)
        {
            await _courses.ReplaceOneAsync(c => c.Id == id, course);
        }

        public async Task DeleteAsync(string id)
        {
            await _courses.DeleteOneAsync(c => c.Id == id);
        }

        public Task<long> CountAsync(string? search = null, string? category = null, string? level = null)
        {
            return _courses.CountDocumentsAsync(BuildFilter(search, category, level));
        }

        public Task<long> CountPublishedAsync(string? search = null, string? category = null, string? level = null)
        {
            return _courses.CountDocumentsAsync(Builders<Course>.Filter.And(
                Builders<Course>.Filter.Eq(c => c.Status, "published"),
                BuildFilter(search, category, level)));
        }

        public Task<long> CountByStatusAsync(string status)
        {
            return _courses.CountDocumentsAsync(Builders<Course>.Filter.Eq(course => course.Status, status));
        }

        private static FilterDefinition<Course> BuildFilter(string? search, string? category, string? level)
        {
            var filters = new List<FilterDefinition<Course>>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var escapedSearch = System.Text.RegularExpressions.Regex.Escape(search.Trim());
                filters.Add(Builders<Course>.Filter.Or(
                    Builders<Course>.Filter.Regex(c => c.Title, new MongoDB.Bson.BsonRegularExpression(escapedSearch, "i")),
                    Builders<Course>.Filter.Regex(c => c.Description, new MongoDB.Bson.BsonRegularExpression(escapedSearch, "i")),
                    Builders<Course>.Filter.Regex(c => c.ShortDescription, new MongoDB.Bson.BsonRegularExpression(escapedSearch, "i"))));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                var normalizedCategory = category.Trim();
                // Match both legacy single-category documents and the new category list.
                filters.Add(Builders<Course>.Filter.Or(
                    Builders<Course>.Filter.Eq("Category.Slug", normalizedCategory),
                    Builders<Course>.Filter.Eq("Categories.Slug", normalizedCategory)));
            }

            if (!string.IsNullOrWhiteSpace(level))
                filters.Add(Builders<Course>.Filter.Eq(c => c.Level, level.Trim()));

            return filters.Count == 0 ? Builders<Course>.Filter.Empty : Builders<Course>.Filter.And(filters);
        }

        private static SortDefinition<Course> GetSort(string? sort) => sort?.ToLowerInvariant() switch
        {
            "title" => Builders<Course>.Sort.Ascending(c => c.Title),
            "price-asc" => Builders<Course>.Sort.Ascending(c => c.Price),
            "price-desc" => Builders<Course>.Sort.Descending(c => c.Price),
            "rating" => Builders<Course>.Sort.Descending(c => c.AverageRating),
            _ => Builders<Course>.Sort.Descending(c => c.CreatedAt)
        };

        public async Task<bool> ExistsAsync(string slug)
        {
            var count = await _courses.CountDocumentsAsync(c => c.Slug == slug);
            return count > 0;
        }

        public async Task UpdateStatsAsync(string courseId, int studentDelta, int ratingDelta)
        {
            var update = Builders<Course>.Update
                .Inc(c => c.TotalStudents, studentDelta)
                .Inc(c => c.TotalReviews, ratingDelta)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            // لو عايز تعيد حساب AverageRating
            if (ratingDelta != 0)
            {
                // هتحسبها في Service مش هنا
            }

            await _courses.UpdateOneAsync(c => c.Id == courseId, update);
        }
     /*   public async Task<long> CountAsync(
            string? search = null,
            string? category = null,
            string? level = null)
        {
            var filter = Builders<Course>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(search))
            {
                filter &= Builders<Course>.Filter.Or(
                    Builders<Course>.Filter.Regex(
                        c => c.Title,
                        new MongoDB.Bson.BsonRegularExpression(search, "i")),

                    Builders<Course>.Filter.Regex(
                        c => c.Description,
                        new MongoDB.Bson.BsonRegularExpression(search, "i"))
                );
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                filter &= Builders<Course>.Filter.Eq(
                    c => c.Category!.Slug,
                    category);
            }

            if (!string.IsNullOrWhiteSpace(level))
            {
                filter &= Builders<Course>.Filter.Eq(
                    c => c.Level,
                    level);
            }

            return await _courses.CountDocumentsAsync(filter);
        }*/
       /* public async Task<long> CountPublishedAsync(
            string? search = null,
            string? category = null,
            string? level = null)
        {
            var filter =
                Builders<Course>.Filter.Eq(
                    c => c.Status,
                    "published");

            if (!string.IsNullOrWhiteSpace(search))
            {
                filter &= Builders<Course>.Filter.Or(
                    Builders<Course>.Filter.Regex(
                        c => c.Title,
                        new MongoDB.Bson.BsonRegularExpression(search, "i")),

                    Builders<Course>.Filter.Regex(
                        c => c.Description,
                        new MongoDB.Bson.BsonRegularExpression(search, "i"))
                );
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                filter &= Builders<Course>.Filter.Eq(
                    c => c.Category!.Slug,
                    category);
            }

            if (!string.IsNullOrWhiteSpace(level))
            {
                filter &= Builders<Course>.Filter.Eq(
                    c => c.Level,
                    level);
            }

            return await _courses.CountDocumentsAsync(filter);
        }*/
    }
}
