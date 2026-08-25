using Educatinal_Platform.Models;
using MongoDB.Driver;

namespace Educatinal_Platform.Repositories
{
    public interface ILessonRepository
    {
        Task<List<Lesson>> GetByCourseIdAsync(string courseId);

        Task<Lesson?> GetByIdAsync(string id);

        Task<Lesson> CreateAsync(Lesson lesson);

        Task UpdateAsync(string id, Lesson lesson);

        Task DeleteAsync(string id);
        Task<long> CountByCourseIdAsync(string courseId);
        Task<int> GetNextOrderIndexAsync(string courseId);
    }
    public class LessonRepository : ILessonRepository
    {
        private readonly IMongoCollection<Lesson> _lessons;

        public LessonRepository(IMongoDatabase database)
        {
            _lessons = database.GetCollection<Lesson>("Lessons");
        }

        public async Task<List<Lesson>> GetByCourseIdAsync(string courseId)
        {
            return await _lessons
                .Find(l => l.CourseId == courseId)
                .SortBy(l => l.OrderIndex)
                .ToListAsync();
        }

        public async Task<Lesson?> GetByIdAsync(string id)
        {
            return await _lessons
                .Find(l => l.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<Lesson> CreateAsync(Lesson lesson)
        {
            await _lessons.InsertOneAsync(lesson);
            return lesson;
        }

        public async Task UpdateAsync(string id, Lesson lesson)
        {
            await _lessons.ReplaceOneAsync(
                l => l.Id == id,
                lesson);
        }

        public async Task DeleteAsync(string id)
        {
            await _lessons.DeleteOneAsync(
                l => l.Id == id);
        }

        public async Task<long> CountByCourseIdAsync(string courseId)
        {
            return await _lessons.CountDocumentsAsync(
                l => l.CourseId == courseId);
        }
        public async Task<int> GetNextOrderIndexAsync(string courseId)
        {
            var lastLesson = await _lessons
                .Find(l => l.CourseId == courseId)
                .SortByDescending(l => l.OrderIndex)
                .FirstOrDefaultAsync();

            if (lastLesson == null)
                return 1;

            return lastLesson.OrderIndex + 1;
        }
    }
}
