using AutoMapper;
using Educatinal_Platform.DTOs;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using EduPlatformAPI.Repositories;
using Stripe;

namespace Educatinal_Platform.Services
{
    public interface ILessonService
    {
        Task<List<LessonDto>> GetByCourseIdAsync(string courseId);

        Task<LessonDto?> GetByIdAsync(string id);

        Task<LessonDto> CreateAsync(
            string courseId,
            CreateLessonDto dto);

        Task UpdateAsync(
            string id,
            UpdateLessonDto dto);
        Task UpdateResourceAsync(
            string lessonId,
            string resourceId,
            UpdateLessonResourceDto dto);

        Task DeleteAsync(string id);
        Task DeleteResourceAsync(
            string lessonId,
            string resourceId); 


    }

    public class LessonService : ILessonService
    {
        private readonly ILessonRepository _lessonRepo;
        private readonly ICourseRepository _courseRepo;
        private readonly IMapper _mapper;
        private readonly IEnrollmentRepository _enrollmentRepo;
        private readonly IFileService _fileService;
        public LessonService(
            ILessonRepository lessonRepo,
            ICourseRepository courseRepo,
             IEnrollmentRepository enrollmentRepo,
            IMapper mapper,
            IFileService fileService)
        {
            _lessonRepo = lessonRepo;
            _courseRepo = courseRepo;
            _enrollmentRepo = enrollmentRepo;
            _mapper = mapper;
            _fileService = fileService;
        }

        public async Task<List<LessonDto>> GetByCourseIdAsync(
            string courseId)
        {
            var course = await _courseRepo.GetByIdAsync(courseId);

            if (course == null)
                throw new KeyNotFoundException(
                    "Course not found.");

            var lessons =
                await _lessonRepo.GetByCourseIdAsync(courseId);

            return _mapper.Map<List<LessonDto>>(lessons);
        }

        public async Task<LessonDto?> GetByIdAsync(string id)
        {
            var lesson = await _lessonRepo.GetByIdAsync(id);

            if (lesson == null)
                return null;

            return _mapper.Map<LessonDto>(lesson);
        }

        public async Task<LessonDto> CreateAsync(
            string courseId,
            CreateLessonDto dto)
        {
            var course = await _courseRepo.GetByIdAsync(courseId);

            if (course == null)
                throw new KeyNotFoundException(
                    "Course not found.");

            var nextOrderIndex =
                await _lessonRepo.GetNextOrderIndexAsync(courseId);

            var lesson = new Lesson
            {
                CourseId = courseId,
                OrderIndex = nextOrderIndex,
                Title = dto.Title.Trim(),
                ContentText = dto.ContentText,
                IsPreview = dto.IsPreview,
                Resources = dto.Resources.Select(r =>
                    new LessonResource
                    {
                        Title = r.Title,
                        FileUrl = r.FileUrl,
                        FileType = r.FileType
                    }).ToList(),
                CreatedAt = DateTime.UtcNow
            };
            if (dto.Video != null)
            {
                lesson.VideoUrl =
                    await _fileService.UploadAsync(
                        dto.Video,
                        "lessons",
                        new[] { ".mp4", ".webm", ".mov" },
                        500 * 1024 * 1024);
            }

            var createdLesson =
                await _lessonRepo.CreateAsync(lesson);


            course.TotalLessons++;
            
            course.TotalHours +=
                (decimal)dto.VideoDurationSeconds / 3600m;

            course.UpdatedAt = DateTime.UtcNow;

            await _courseRepo.UpdateAsync(
                courseId,
                course);

            await RecalculateEnrollmentsAsync(courseId);

            return _mapper.Map<LessonDto>(
                createdLesson);
        }

        public async Task UpdateAsync(
    string id,
    UpdateLessonDto dto)
        {
            // 1. Get Lesson
            var lesson =
                await _lessonRepo.GetByIdAsync(id);

            if (lesson == null)
                throw new KeyNotFoundException(
                    "Lesson not found.");

            // Keep old values before modifying the lesson
            var oldDurationSeconds =
                lesson.VideoDurationSeconds;

            var oldVideoUrl =
                lesson.VideoUrl;

            // 2. Update basic information

            if (dto.Title != null)
            {
                var title = dto.Title.Trim();

                if (string.IsNullOrWhiteSpace(title))
                    throw new ArgumentException(
                        "Lesson title cannot be empty.");

                lesson.Title = title;
            }

            if (dto.ContentText != null)
                lesson.ContentText = dto.ContentText;

            if (dto.IsPreview.HasValue)
                lesson.IsPreview = dto.IsPreview.Value;

            // 3. Update Video
            if (dto.Video != null)
            {
                var newVideoUrl =
                    await _fileService.UploadAsync(
                        dto.Video,
                        "lessons/videos",
                        new[] { ".mp4", ".webm", ".mov" },
                        500 * 1024 * 1024);

                lesson.VideoUrl = newVideoUrl;
            }

            // 4. Update Video Duration
            if (dto.VideoDurationSeconds.HasValue)
            {
                lesson.VideoDurationSeconds =
                    dto.VideoDurationSeconds.Value;
            }

            // 5. Update Resources
            if (dto.Resources != null)
            {
                lesson.Resources = dto.Resources.Select(r =>
                    new LessonResource
                    {
                        Title = r.Title.Trim(),
                        FileUrl = r.FileUrl,
                        FileType = r.FileType
                    }).ToList();
            }

            // 6. Update Lesson timestamp
            lesson.UpdatedAt = DateTime.UtcNow;

            // 7. Save Lesson
            await _lessonRepo.UpdateAsync(
                id,
                lesson);

            // 8. Delete old video
            if (dto.Video != null &&
                !string.IsNullOrEmpty(oldVideoUrl))
            {
                await _fileService.DeleteAsync(
                    oldVideoUrl);
            }

            // 9. Update Course statistics
            if (dto.VideoDurationSeconds.HasValue)
            {
                var course =
                    await _courseRepo.GetByIdAsync(
                        lesson.CourseId);

                if (course == null)
                    throw new KeyNotFoundException(
                        "Course not found.");

                var durationDifference =
                    lesson.VideoDurationSeconds -
                    oldDurationSeconds;

                course.TotalHours +=
                    (decimal)durationDifference / 3600m;

                course.UpdatedAt = DateTime.UtcNow;

                await _courseRepo.UpdateAsync(
                    course.Id,
                    course);
            }
        }

        public async Task DeleteAsync(string id)
        {
            var lesson =
                await _lessonRepo.GetByIdAsync(id);

            if (lesson == null)
                throw new KeyNotFoundException(
                    "Lesson not found.");

            var course =
                await _courseRepo.GetByIdAsync(
                    lesson.CourseId);

            if (course == null)
                throw new KeyNotFoundException(
                    "Course not found.");

            await _lessonRepo.DeleteAsync(id);

            if (course.TotalLessons > 0)
                course.TotalLessons--;

            course.TotalHours -=
                (decimal)lesson.VideoDurationSeconds / 3600m;

            // حماية من القيم السالبة بسبب بيانات قديمة
            if (course.TotalHours < 0)
                course.TotalHours = 0;

            course.UpdatedAt = DateTime.UtcNow;

            await _courseRepo.UpdateAsync(
                course.Id,
                course);

            await RecalculateEnrollmentsAsync(course.Id);

        }

        private async Task RecalculateEnrollmentsAsync(string courseId)
        {
            var enrollments =
                await _enrollmentRepo.GetByCourseIdAsync(courseId);

            var lessons =
                await _lessonRepo.GetByCourseIdAsync(courseId);

            var totalLessons = lessons.Count;

            foreach (var enrollment in enrollments)
            {
                var completedLessons =
                    enrollment.WatchedLessons
                        .Count(w =>
                            w.IsCompleted &&
                            lessons.Any(l => l.Id == w.LessonId));

                if (totalLessons == 0)
                {
                    enrollment.ProgressPercent = 0;
                    enrollment.Status = "active";
                    enrollment.CompletedAt = null;
                }
                else
                {
                    enrollment.ProgressPercent =
                        Math.Round(
                            (decimal)completedLessons /
                            totalLessons * 100,
                            2);

                    if (completedLessons == totalLessons)
                    {
                        enrollment.ProgressPercent = 100;
                        enrollment.Status = "completed";

                        if (!enrollment.CompletedAt.HasValue)
                            enrollment.CompletedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        enrollment.Status = "active";
                        enrollment.CompletedAt = null;
                    }
                }

                await _enrollmentRepo.UpdateAsync(
                    enrollment.Id,
                    enrollment);
            }
        }

        public async Task UpdateResourceAsync(
            string lessonId,
            string resourceId,
            UpdateLessonResourceDto dto)
        {
            var lesson =
                await _lessonRepo.GetByIdAsync(lessonId);

            if (lesson == null)
                throw new KeyNotFoundException(
                    "Lesson not found.");

            var resource =
                lesson.Resources.FirstOrDefault(
                    r => r.Id == resourceId);

            if (resource == null)
                throw new KeyNotFoundException(
                    "Resource not found.");

            // 1. Update title
            if (dto.Title != null)
            {
                var title = dto.Title.Trim();

                if (string.IsNullOrWhiteSpace(title))
                    throw new ArgumentException(
                        "Resource title cannot be empty.");

                resource.Title = title;
            }

            // 2. Replace file
            if (dto.File != null)
            {
                var oldFileUrl = resource.FileUrl;

                var newFileUrl =
                    await _fileService.UploadAsync(
                        dto.File,
                        "lessons/resources",
                        new[]
                        {
                    ".pdf",
                    ".doc",
                    ".docx",
                    ".zip",
                    ".rar",
                    ".txt"
                        },
                        50 * 1024 * 1024);

                resource.FileUrl = newFileUrl;
                resource.FileType = dto.File.ContentType;

                // 3. Save Lesson first
                await _lessonRepo.UpdateAsync(
                    lessonId,
                    lesson);

                // 4. Delete old file
                if (!string.IsNullOrEmpty(oldFileUrl))
                {
                    await _fileService.DeleteAsync(
                        oldFileUrl);
                }

                return;
            }

            // 5. Save title-only update
            await _lessonRepo.UpdateAsync(
                lessonId,
                lesson);
        }
        public async Task DeleteResourceAsync(
    string lessonId,
    string resourceId)
        {
            var lesson =
                await _lessonRepo.GetByIdAsync(lessonId);

            if (lesson == null)
                throw new KeyNotFoundException(
                    "Lesson not found.");

            var resource =
                lesson.Resources.FirstOrDefault(
                    r => r.Id == resourceId);

            if (resource == null)
                throw new KeyNotFoundException(
                    "Resource not found.");

            var fileUrl = resource.FileUrl;

            lesson.Resources.Remove(resource);

            await _lessonRepo.UpdateAsync(
                lessonId,
                lesson);

            if (!string.IsNullOrEmpty(fileUrl))
            {
                await _fileService.DeleteAsync(
                    fileUrl);
            }
        }
    }
}
