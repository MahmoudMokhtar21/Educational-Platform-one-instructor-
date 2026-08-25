using Educatinal_Platform.Models;
using MongoDB.Driver;

namespace Educatinal_Platform.Repositories
{
    public interface IReviewRepository
    {
        Task<Review> CreateAsync(Review review);

        Task<Review?> GetByIdAsync(string id);

        Task<Review?> GetByStudentAndCourseAsync(
            string studentId,
            string courseId);

        Task<List<Review>> GetByCourseIdAsync(
            string courseId);

        Task UpdateAsync(
            string id,
            Review review);

        Task DeleteAsync(string id);
    }

    public class ReviewRepository : IReviewRepository
    {
        private readonly IMongoCollection<Review> _reviews;

        public ReviewRepository(IMongoDatabase database)
        {
            _reviews =
                database.GetCollection<Review>(
                    "Reviews");
        }

        public async Task<Review> CreateAsync(
            Review review)
        {
            await _reviews.InsertOneAsync(review);

            return review;
        }

        public async Task<Review?> GetByIdAsync(
            string id)
        {
            return await _reviews
                .Find(r => r.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<Review?> GetByStudentAndCourseAsync(
            string studentId,
            string courseId)
        {
            return await _reviews
                .Find(r =>
                    r.StudentId == studentId &&
                    r.CourseId == courseId)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Review>> GetByCourseIdAsync(
            string courseId)
        {
            return await _reviews
                .Find(r => r.CourseId == courseId)
                .SortByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateAsync(
            string id,
            Review review)
        {
            await _reviews.ReplaceOneAsync(
                r => r.Id == id,
                review);
        }

        public async Task DeleteAsync(
            string id)
        {
            await _reviews.DeleteOneAsync(
                r => r.Id == id);
        }
    }
}