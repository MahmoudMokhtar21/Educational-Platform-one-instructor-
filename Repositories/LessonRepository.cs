using Educatinal_Platform.Models;
using MongoDB.Driver;

namespace Educatinal_Platform.Repositories
{
    public interface ILessonRepository
    {
        Task<List<Lesson>> GetByCourseIdAsync(string courseId);
        Task<List<Lesson>> GetByTrackIdAsync(string courseId, string trackId);
        Task<Lesson?> GetByIdAsync(string id);
        Task<Lesson> CreateAsync(Lesson lesson);
        Task UpdateAsync(string id, Lesson lesson);
        Task DeleteAsync(string id);
        Task<long> CountByCourseIdAsync(string courseId);
        Task<int> GetNextOrderIndexAsync(string courseId);
        Task<int> GetNextOrderIndexAsync(string courseId, string trackId);
        Task ReorderAsync(string courseId, IReadOnlyDictionary<string, int> orderByLessonId);
        Task ReorderAsync(string courseId, string trackId, IReadOnlyDictionary<string, int> orderByLessonId);
        Task AssignToTrackAsync(string courseId, string trackId, IReadOnlyCollection<string> lessonIds);
        Task MoveToTrackAsync(string courseId, IReadOnlyCollection<string> lessonIds, string targetTrackId, int firstOrderIndex);
        Task NormalizeTrackOrderAsync(string courseId, string trackId);
    }

    public class LessonRepository : ILessonRepository
    {
        private readonly IMongoCollection<Lesson> _lessons;

        public LessonRepository(IMongoDatabase database)
        {
            _lessons = database.GetCollection<Lesson>("Lessons");
        }

        public Task<List<Lesson>> GetByCourseIdAsync(string courseId) => _lessons
            .Find(lesson => lesson.CourseId == courseId)
            .SortBy(lesson => lesson.OrderIndex)
            .ToListAsync();

        public Task<List<Lesson>> GetByTrackIdAsync(string courseId, string trackId) => _lessons
            .Find(lesson => lesson.CourseId == courseId && lesson.TrackId == trackId)
            .SortBy(lesson => lesson.OrderIndex)
            .ToListAsync();

        public async Task<Lesson?> GetByIdAsync(string id) => await _lessons
            .Find(lesson => lesson.Id == id)
            .FirstOrDefaultAsync();

        public async Task<Lesson> CreateAsync(Lesson lesson)
        {
            await _lessons.InsertOneAsync(lesson);
            return lesson;
        }

        public Task UpdateAsync(string id, Lesson lesson) => _lessons.ReplaceOneAsync(
            existing => existing.Id == id,
            lesson);

        public Task DeleteAsync(string id) => _lessons.DeleteOneAsync(lesson => lesson.Id == id);

        public Task<long> CountByCourseIdAsync(string courseId) => _lessons.CountDocumentsAsync(
            lesson => lesson.CourseId == courseId);

        public Task<int> GetNextOrderIndexAsync(string courseId) => GetNextOrderIndexAsync(
            Builders<Lesson>.Filter.Eq(lesson => lesson.CourseId, courseId));

        public Task<int> GetNextOrderIndexAsync(string courseId, string trackId) => GetNextOrderIndexAsync(
            Builders<Lesson>.Filter.And(
                Builders<Lesson>.Filter.Eq(lesson => lesson.CourseId, courseId),
                Builders<Lesson>.Filter.Eq(lesson => lesson.TrackId, trackId)));

        public Task ReorderAsync(string courseId, IReadOnlyDictionary<string, int> orderByLessonId) => ReorderAsync(
            Builders<Lesson>.Filter.Eq(lesson => lesson.CourseId, courseId),
            orderByLessonId);

        public Task ReorderAsync(string courseId, string trackId, IReadOnlyDictionary<string, int> orderByLessonId) => ReorderAsync(
            Builders<Lesson>.Filter.And(
                Builders<Lesson>.Filter.Eq(lesson => lesson.CourseId, courseId),
                Builders<Lesson>.Filter.Eq(lesson => lesson.TrackId, trackId)),
            orderByLessonId);

        public Task AssignToTrackAsync(string courseId, string trackId, IReadOnlyCollection<string> lessonIds)
        {
            if (lessonIds.Count == 0)
                return Task.CompletedTask;

            var filter = Builders<Lesson>.Filter.And(
                Builders<Lesson>.Filter.Eq(lesson => lesson.CourseId, courseId),
                Builders<Lesson>.Filter.In(lesson => lesson.Id, lessonIds));
            var update = Builders<Lesson>.Update
                .Set(lesson => lesson.TrackId, trackId)
                .Set(lesson => lesson.UpdatedAt, DateTime.UtcNow);
            return _lessons.UpdateManyAsync(filter, update);
        }

        public async Task MoveToTrackAsync(string courseId, IReadOnlyCollection<string> lessonIds, string targetTrackId, int firstOrderIndex)
        {
            if (lessonIds.Count == 0)
                return;

            var now = DateTime.UtcNow;
            var updates = lessonIds.Select((lessonId, index) => new UpdateOneModel<Lesson>(
                Builders<Lesson>.Filter.And(
                    Builders<Lesson>.Filter.Eq(lesson => lesson.CourseId, courseId),
                    Builders<Lesson>.Filter.Eq(lesson => lesson.Id, lessonId)),
                Builders<Lesson>.Update
                    .Set(lesson => lesson.TrackId, targetTrackId)
                    .Set(lesson => lesson.OrderIndex, firstOrderIndex + index)
                    .Set(lesson => lesson.UpdatedAt, now))).ToList();

            await _lessons.BulkWriteAsync(updates);
        }

        public async Task NormalizeTrackOrderAsync(string courseId, string trackId)
        {
            var lessons = await GetByTrackIdAsync(courseId, trackId);
            if (lessons.Count == 0)
                return;

            await ReorderAsync(
                courseId,
                trackId,
                lessons.Select((lesson, index) => new { lesson.Id, OrderIndex = index + 1 })
                    .ToDictionary(item => item.Id, item => item.OrderIndex));
        }

        private async Task<int> GetNextOrderIndexAsync(FilterDefinition<Lesson> filter)
        {
            var lastLesson = await _lessons.Find(filter)
                .SortByDescending(lesson => lesson.OrderIndex)
                .FirstOrDefaultAsync();
            return lastLesson == null ? 1 : lastLesson.OrderIndex + 1;
        }

        private async Task ReorderAsync(FilterDefinition<Lesson> courseAndTrackFilter, IReadOnlyDictionary<string, int> orderByLessonId)
        {
            if (orderByLessonId.Count == 0)
                return;

            var now = DateTime.UtcNow;
            var updates = orderByLessonId.Select(item => new UpdateOneModel<Lesson>(
                Builders<Lesson>.Filter.And(
                    courseAndTrackFilter,
                    Builders<Lesson>.Filter.Eq(lesson => lesson.Id, item.Key)),
                Builders<Lesson>.Update
                    .Set(lesson => lesson.OrderIndex, item.Value)
                    .Set(lesson => lesson.UpdatedAt, now))).ToList();

            await _lessons.BulkWriteAsync(updates);
        }
    }
}
