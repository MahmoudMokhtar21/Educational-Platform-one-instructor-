using AutoMapper;
using Educatinal_Platform.DTOs;
using Educatinal_Platform.Exceptions;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using EduPlatformAPI.Repositories;

namespace Educatinal_Platform.Services
{
    public interface ILessonService
    {
        Task<List<LessonDto>> GetByCourseIdAsync(string courseId, bool includeStorageUrls = false);

        Task<LessonDto?> GetByIdAsync(string id, bool includeStorageUrls = false);

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
        Task ReorderAsync(string courseId, ReorderLessonsDto dto);
        Task ReorderInTrackAsync(string courseId, string trackId, ReorderLessonsDto dto);
        Task MoveToTrackAsync(string courseId, string lessonId, MoveLessonToTrackDto dto);


    }

    public class LessonService : ILessonService
    {
        private readonly ILessonRepository _lessonRepo;
        private readonly ICourseRepository _courseRepo;
        private readonly IMapper _mapper;
        private readonly IEnrollmentRepository _enrollmentRepo;
        private readonly IFileService _fileService;
        private readonly ITrackService _trackService;
        public LessonService(
            ILessonRepository lessonRepo,
            ICourseRepository courseRepo,
             IEnrollmentRepository enrollmentRepo,
            IMapper mapper,
            IFileService fileService,
            ITrackService trackService)
        {
            _lessonRepo = lessonRepo;
            _courseRepo = courseRepo;
            _enrollmentRepo = enrollmentRepo;
            _mapper = mapper;
            _fileService = fileService;
            _trackService = trackService;
        }

        public async Task<List<LessonDto>> GetByCourseIdAsync(
            string courseId,
            bool includeStorageUrls = false)
        {
            var course = await _courseRepo.GetByIdAsync(courseId);

            if (course == null)
                throw new KeyNotFoundException(
                    "Course not found.");

            var lessons =
                await _lessonRepo.GetByCourseIdAsync(courseId);

            return MapLessons(lessons, includeStorageUrls);
        }

        public async Task<LessonDto?> GetByIdAsync(string id, bool includeStorageUrls = false)
        {
            var lesson = await _lessonRepo.GetByIdAsync(id);

            if (lesson == null)
                return null;

            return MapLesson(lesson, includeStorageUrls);
        }

        public async Task<LessonDto> CreateAsync(
            string courseId,
            CreateLessonDto dto)
        {
            var course = await _courseRepo.GetByIdAsync(courseId);

            if (course == null)
                throw new KeyNotFoundException(
                    "Course not found.");

            var track = await _trackService.ResolveForLessonAsync(courseId, dto.TrackId);
            var nextOrderIndex =
                await _lessonRepo.GetNextOrderIndexAsync(courseId, track.Id);

            var lesson = new Lesson
            {
                CourseId = courseId,
                TrackId = track.Id,
                OrderIndex = nextOrderIndex,
                Title = dto.Title.Trim(),
                ContentText = dto.ContentText,
                VideoDurationSeconds = dto.VideoDurationSeconds,
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
                        maxSize: null);
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

            return MapLesson(createdLesson, includeStorageUrls: false);
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
                        maxSize: null);

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

        public async Task ReorderAsync(string courseId, ReorderLessonsDto dto)
        {
            var course = await _courseRepo.GetByIdAsync(courseId);
            if (course == null) throw new KeyNotFoundException("Course not found.");

            var existingLessons = await _lessonRepo.GetByCourseIdAsync(courseId);
            if (dto.Lessons.Count != existingLessons.Count)
                throw new ArgumentException("The submitted ordering must include every lesson in the course.");

            var submittedIds = dto.Lessons.Select(item => item.LessonId).ToList();
            if (submittedIds.Any(string.IsNullOrWhiteSpace) || submittedIds.Distinct().Count() != submittedIds.Count)
                throw new ArgumentException("Each lesson must appear exactly once.");

            if (!submittedIds.OrderBy(id => id).SequenceEqual(existingLessons.Select(lesson => lesson.Id).OrderBy(id => id)))
                throw new ArgumentException("The submitted lessons do not match this course.");

            var indexes = dto.Lessons.Select(item => item.OrderIndex).OrderBy(index => index).ToList();
            if (!indexes.SequenceEqual(Enumerable.Range(1, existingLessons.Count)))
                throw new ArgumentException("Order indexes must be consecutive and start at 1.");

            await _lessonRepo.ReorderAsync(courseId, dto.Lessons.ToDictionary(item => item.LessonId, item => item.OrderIndex));
        }

        public async Task ReorderInTrackAsync(string courseId, string trackId, ReorderLessonsDto dto)
        {
            var course = await _courseRepo.GetByIdAsync(courseId);
            if (course == null) throw new NotFoundException("Course not found.");

            var track = await _trackService.ResolveForLessonAsync(courseId, trackId);
            var existingLessons = await _lessonRepo.GetByTrackIdAsync(courseId, track.Id);
            if (dto.Lessons.Count != existingLessons.Count)
                throw new BadRequestException("The submitted ordering must include every lesson in the track.");

            var submittedIds = dto.Lessons.Select(item => item.LessonId).ToList();
            if (submittedIds.Any(string.IsNullOrWhiteSpace) || submittedIds.Distinct().Count() != submittedIds.Count)
                throw new BadRequestException("Each lesson must appear exactly once.");

            if (!submittedIds.OrderBy(id => id).SequenceEqual(existingLessons.Select(lesson => lesson.Id).OrderBy(id => id)))
                throw new BadRequestException("The submitted lessons do not match this track.");

            var indexes = dto.Lessons.Select(item => item.OrderIndex).OrderBy(index => index).ToList();
            if (!indexes.SequenceEqual(Enumerable.Range(1, existingLessons.Count)))
                throw new BadRequestException("Order indexes must be consecutive and start at 1.");

            await _lessonRepo.ReorderAsync(courseId, track.Id, dto.Lessons.ToDictionary(item => item.LessonId, item => item.OrderIndex));
        }

        public async Task MoveToTrackAsync(string courseId, string lessonId, MoveLessonToTrackDto dto)
        {
            var lesson = await _lessonRepo.GetByIdAsync(lessonId);
            if (lesson == null || !string.Equals(lesson.CourseId, courseId, StringComparison.Ordinal))
                throw new NotFoundException("Lesson not found.");

            var targetTrack = await _trackService.ResolveForLessonAsync(courseId, dto.TrackId);
            if (string.Equals(lesson.TrackId, targetTrack.Id, StringComparison.Ordinal))
                return;

            var sourceTrackId = lesson.TrackId;
            var nextOrderIndex = await _lessonRepo.GetNextOrderIndexAsync(courseId, targetTrack.Id);
            await _lessonRepo.MoveToTrackAsync(courseId, new[] { lesson.Id }, targetTrack.Id, nextOrderIndex);

            if (!string.IsNullOrWhiteSpace(sourceTrackId))
                await _lessonRepo.NormalizeTrackOrderAsync(courseId, sourceTrackId);
            await _lessonRepo.NormalizeTrackOrderAsync(courseId, targetTrack.Id);
        }

        private List<LessonDto> MapLessons(IEnumerable<Lesson> lessons, bool includeStorageUrls) =>
            lessons.Select(lesson => MapLesson(lesson, includeStorageUrls)).ToList();

        private LessonDto MapLesson(Lesson lesson, bool includeStorageUrls)
        {
            var dto = _mapper.Map<LessonDto>(lesson);

            for (var index = 0; index < lesson.Resources.Count && index < dto.Resources.Count; index++)
            {
                var source = lesson.Resources[index];
                if (includeStorageUrls || IsSafeExternalResourceUrl(source.FileUrl))
                    dto.Resources[index].FileUrl = source.FileUrl;
            }

            if (includeStorageUrls)
                dto.VideoUrl = lesson.VideoUrl;

            return dto;
        }

        private static bool IsSafeExternalResourceUrl(string? value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

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
