// Repositories/IEnrollmentRepository.cs

using Educatinal_Platform.Models;
using MongoDB.Bson;
using MongoDB.Driver;


namespace EduPlatformAPI.Repositories
{
    public interface IEnrollmentRepository
    {
        Task<Enrollment> CreateAsync(Enrollment enrollment);
        Task<Enrollment?> GetByIdAsync(string id);
        Task<Enrollment?> GetByStudentAndCourseAsync(string studentId, string courseId);
        Task<List<Enrollment>> GetByStudentIdAsync(string studentId);
        Task<List<Enrollment>> GetByCourseIdAsync(string courseId);
        Task UpdateAsync(string id, Enrollment enrollment);
        Task DeleteAsync(string id);

        Task<bool> IsStudentEnrolledAsync(string studentId, string courseId);
        Task UpdateProgressAsync(string enrollmentId, decimal progressPercent);
        Task<List<Enrollment>> GetActiveEnrollmentsByStudentAsync(string studentId);
        Task UpdateWatchedLessonAsync(
        string enrollmentId,
        WatchedLesson watchedLesson);

        Task<Enrollment?> GetActiveByStudentAndCourseAsync(
            string studentId,
            string courseId);
    }
    public class EnrollmentRepository : IEnrollmentRepository
    {
        private readonly IMongoCollection<Enrollment> _enrollments;

        public EnrollmentRepository(IMongoDatabase database)
        {
            _enrollments = database.GetCollection<Enrollment>("Enrollments");
        }

        public async Task<Enrollment> CreateAsync(Enrollment enrollment)
        {
            if (string.IsNullOrEmpty(enrollment.Id))
                enrollment.Id = ObjectId.GenerateNewId().ToString();

            await _enrollments.InsertOneAsync(enrollment);
            return enrollment;
        }

        public async Task<Enrollment?> GetByIdAsync(string id)
        {
            return await _enrollments.Find(e => e.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Enrollment?> GetByStudentAndCourseAsync(string studentId, string courseId)
        {
            return await _enrollments
                .Find(e => e.StudentId == studentId && e.CourseId == courseId)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Enrollment>> GetByStudentIdAsync(string studentId)
        {
            return await _enrollments
                .Find(e => e.StudentId == studentId)
                .ToListAsync();
        }

        public async Task<List<Enrollment>> GetByCourseIdAsync(string courseId)
        {
            return await _enrollments
                .Find(e => e.CourseId == courseId)
                .ToListAsync();
        }

        public async Task UpdateAsync(string id, Enrollment enrollment)
        {
            await _enrollments.ReplaceOneAsync(e => e.Id == id, enrollment);
        }

        public async Task DeleteAsync(string id)
        {
            await _enrollments.DeleteOneAsync(e => e.Id == id);
        }

        public async Task<bool> IsStudentEnrolledAsync(string studentId, string courseId)
        {
            return await _enrollments
                .Find(e =>
                    e.StudentId == studentId &&
                    e.CourseId == courseId)
                .Limit(1)
                .AnyAsync();
        }

        public async Task UpdateProgressAsync(string enrollmentId, decimal progressPercent)
        {
            var update = Builders<Enrollment>.Update
                .Set(e => e.ProgressPercent, progressPercent)
                .Set(e => e.LastWatchedAt, DateTime.UtcNow);

            await _enrollments.UpdateOneAsync(e => e.Id == enrollmentId, update);
        }

        public async Task<List<Enrollment>> GetActiveEnrollmentsByStudentAsync(string studentId)
        {
            return await _enrollments
                .Find(e => e.StudentId == studentId && e.Status == "active")
                .ToListAsync();
        }
        public async Task UpdateWatchedLessonAsync(
            string enrollmentId,
            WatchedLesson watchedLesson)
        {
            var enrollment =
                await _enrollments
                    .Find(e => e.Id == enrollmentId)
                    .FirstOrDefaultAsync();

            if (enrollment == null)
                return;

            var existingLesson =
                enrollment.WatchedLessons
                    .FirstOrDefault(w =>
                        w.LessonId == watchedLesson.LessonId);

            if (existingLesson == null)
            {
                enrollment.WatchedLessons.Add(watchedLesson);
            }
            else
            {
                existingLesson.WatchedAt =
                    watchedLesson.WatchedAt;

                existingLesson.WatchDurationSeconds =
                    watchedLesson.WatchDurationSeconds;

                existingLesson.IsCompleted =
                    watchedLesson.IsCompleted;

                existingLesson.LessonTitle =
                    watchedLesson.LessonTitle;
            }

            enrollment.LastWatchedLessonId =
                watchedLesson.LessonId;

            enrollment.LastWatchedAt =
                DateTime.UtcNow;

            await _enrollments.ReplaceOneAsync(
                e => e.Id == enrollmentId,
                enrollment);
        }
        public async Task<Enrollment?> GetActiveByStudentAndCourseAsync(
            string studentId,
            string courseId)
        {
            return await _enrollments
                .Find(e =>
                    e.StudentId == studentId &&
                    e.CourseId == courseId &&
                    e.Status == "active")
                .FirstOrDefaultAsync();
        }
    }
}
