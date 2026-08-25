using Educatinal_Platform.Models;
using MongoDB.Driver;
using System.Reflection.Emit;

namespace Educatinal_Platform.Repositories
{
    public interface ICourseRepository
    {
        Task<List<Course>> GetAllAsync(int page, int limit,string? search = null, string? category = null,string? level = null);
        Task<Course?> GetByIdAsync(string id);
        Task<Course?> GetBySlugAsync(string slug);
        Task<List<Course>> GetPublishedAsync(int page,int limit,string? search = null,string? category = null, string? level = null);
        Task<Course> CreateAsync(Course course);
        Task UpdateAsync(string id, Course course);
        Task DeleteAsync(string id);
        Task<long> CountAsync();
        Task<long> CountPublishedAsync();
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
            string? level = null)
        {
            var filter = Builders<Course>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(search))
            {
                filter = Builders<Course>.Filter.Or(
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

            return await _courses
                .Find(filter)
                .Skip((page - 1) * limit)
                .Limit(limit)
                .SortByDescending(c => c.CreatedAt)
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
            string? level = null)
        {
            var filters = new List<FilterDefinition<Course>>
            {
                Builders<Course>.Filter.Eq(c => c.Status, "published")
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                filters.Add(
                    Builders<Course>.Filter.Or(
                        Builders<Course>.Filter.Regex(
                            c => c.Title,
                            new MongoDB.Bson.BsonRegularExpression(search, "i")),

                        Builders<Course>.Filter.Regex(
                            c => c.Description,
                            new MongoDB.Bson.BsonRegularExpression(search, "i"))
                    )
                );
            }
            if (!string.IsNullOrWhiteSpace(category))
            {
                filters.Add(
                    Builders<Course>.Filter.Eq(
                        c => c.Category!.Slug,
                        category));
            }

            if (!string.IsNullOrWhiteSpace(level))
            {
                filters.Add(
                    Builders<Course>.Filter.Eq(
                        c => c.Level,
                        level));
            }

            var filter =
                Builders<Course>.Filter.And(filters);

            return await _courses
                .Find(filter)
                .Skip((page - 1) * limit)
                .Limit(limit)
                .SortByDescending(c => c.CreatedAt)
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

        public async Task<long> CountAsync()
        {
            return await _courses.CountDocumentsAsync(_ => true);
        }

        public async Task<long> CountPublishedAsync()
        {
            return await _courses.CountDocumentsAsync(c => c.Status == "published");
        }

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
