using AutoMapper;
using Educatinal_Platform.DTOs;
using Educatinal_Platform.Exceptions;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using EduPlatformAPI.Repositories;

namespace Educatinal_Platform.Services
{
    // Services/ICourseService.cs

    
        public interface ICourseService
        {
            Task<PagedResult<CourseResponseDto>> GetPublishedCoursesAsync(int page, int limit);
            Task<CourseDetailDto?> GetCourseDetailAsync(string slug, string? studentId);
            Task<Course> CreateCourseAsync(CreateCourseDto dto);
            Task UpdateCourseAsync(string id, UpdateCourseDto dto);
            Task DeleteCourseAsync(string id);
            Task PublishCourseAsync(string id);
            //Task<PaginatedResult<Course>> GetAllAsync(int page, int limit);
            //Task<PaginatedResult<Course>> GetPublishedAsync( int page, int limit);          
           
    }

        public class PagedResult<T>
        {
            public List<T> Items { get; set; } = new();
            public int TotalItems { get; set; }
            public int TotalPages { get; set; }
            public int CurrentPage { get; set; }
            public int PageSize { get; set; }
        }
    // Services/CourseService.cs

    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new();

        public int Page { get; set; }

        public int Limit { get; set; }

        public long TotalItems { get; set; }

        public int TotalPages =>
            (int)Math.Ceiling((double)TotalItems / Limit);
    }

    public class CourseService : ICourseService
    {
        private readonly ICourseRepository _courseRepo;
        private readonly IEnrollmentRepository _enrollmentRepo;
        private readonly ILessonRepository _lessonRepo;
        private readonly IMapper _mapper;
        private readonly IFileService _fileService;

        public CourseService(
            ICourseRepository courseRepo,
            IEnrollmentRepository enrollmentRepo,
            IMapper mapper,
            ILessonRepository lessonRepo,
            IFileService fileService
           )
        {
            _courseRepo = courseRepo;
            _enrollmentRepo = enrollmentRepo;
            _mapper = mapper;
            _lessonRepo = lessonRepo;
            _fileService = fileService;
        }

        public async Task<PagedResult<CourseResponseDto>> GetPublishedCoursesAsync(int page, int limit)
        {

            if (page < 1)
                throw new BadRequestException("Page number must be greater than zero.");

            if (limit < 1 || limit > 100)
                throw new BadRequestException("Limit must be between 1 and 100.");

            var courses = await _courseRepo.GetPublishedAsync(page, limit);
            var totalItems = await _courseRepo.CountPublishedAsync();

            var dtos = _mapper.Map<List<CourseResponseDto>>(courses);

            return new PagedResult<CourseResponseDto>
            {
                Items = dtos,
                TotalItems = (int)totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)limit),
                CurrentPage = page,
                PageSize = limit
            };
        }

        public async Task<CourseDetailDto?> GetCourseDetailAsync(string slug, string? studentId)
        {

            if (string.IsNullOrWhiteSpace(slug))
                throw new BadRequestException("Course slug is required.");

            var course = await _courseRepo.GetBySlugAsync(slug);

            if (course == null)
                throw new NotFoundException("Course not found.");
            if (course.Status != "published")
                throw new BadRequestException("Course is not published yet.");

            var dto = _mapper.Map<CourseDetailDto>(course);

            var lessons = await _lessonRepo.GetByCourseIdAsync(course.Id);
            dto.Lessons = _mapper.Map<List<LessonDto>>(lessons);
            if (!string.IsNullOrEmpty(studentId))
            {
                var enrollment = await _enrollmentRepo.
                    GetActiveByStudentAndCourseAsync(studentId, course.Id);
                if (enrollment != null)
                {
                    dto.IsEnrolled = true;
                    dto.ProgressPercent = enrollment.ProgressPercent;

                    var watchedLessons = enrollment.WatchedLessons
                                        .ToDictionary(x => x.LessonId);
                    foreach (var lesson in dto.Lessons)
                    {
                        if (watchedLessons.TryGetValue(lesson.Id, out var watched))
                        {
                            lesson.IsWatched = true;
                            lesson.IsCompleted = watched.IsCompleted;
                        }
                    }
                }
            }

            return dto;
        }

        public async Task<Course> CreateCourseAsync(CreateCourseDto dto)
        {
            var course = _mapper.Map<Course>(dto);

            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new BadRequestException(
                    "Course title is required.");

            var originalSlug = course.Slug;
            var slug = originalSlug;
            var counter = 1;

            while (await _courseRepo.ExistsAsync(slug))
            {
                slug = $"{originalSlug}-{counter}";
                counter++;
            }

            course.Slug = slug;
            if (dto.Thumbnail != null)
            {
                course.ThumbnailUrl =
                    await _fileService.UploadAsync(
                        dto.Thumbnail,
                        "thumbnails",
                        new[] { ".jpg", ".jpeg", ".png", ".webp" },
                        5 * 1024 * 1024);
            }

            if (dto.Trailer != null)
            {
                course.TrailerUrl =
                    await _fileService.UploadAsync(
                        dto.Trailer,
                        "trailers",
                        new[] { ".mp4", ".webm", ".mov" },
                        100 * 1024 * 1024);
            }

            course.Status = "draft";
            course.CreatedAt = DateTime.UtcNow;
            course.UpdatedAt = DateTime.UtcNow;

            return await _courseRepo.CreateAsync(course);
        }
        public async Task UpdateCourseAsync(
    string id,
    UpdateCourseDto dto)
        {
            var course = await _courseRepo.GetByIdAsync(id);

            if (course == null)
                throw new NotFoundException("Course not found.");

            course.Title = dto.Title;
            course.Description = dto.Description;
            course.Price = dto.Price;
            course.Level = dto.Level;

            if (dto.Status != null)
                course.Status = dto.Status;

            if (dto.Tags != null)
                course.Tags = dto.Tags;

            if (dto.Thumbnail != null)
            {
                var oldThumbnailUrl = course.ThumbnailUrl;

                course.ThumbnailUrl =
                    await _fileService.UploadAsync(
                        dto.Thumbnail,
                        "thumbnails",
                        new[] { ".jpg", ".jpeg", ".png", ".webp" },
                        5 * 1024 * 1024);

                if (!string.IsNullOrWhiteSpace(oldThumbnailUrl))
                    await _fileService.DeleteAsync(oldThumbnailUrl);
            }

            if (dto.Trailer != null)
            {
                var oldTrailerUrl = course.TrailerUrl;

                course.TrailerUrl =
                    await _fileService.UploadAsync(
                        dto.Trailer,
                        "trailers",
                        new[] { ".mp4", ".webm", ".mov" },
                        100 * 1024 * 1024);

                if (!string.IsNullOrWhiteSpace(oldTrailerUrl))
                    await _fileService.DeleteAsync(oldTrailerUrl);
            }

            course.UpdatedAt = DateTime.UtcNow;

            await _courseRepo.UpdateAsync(id, course);
        }
        //public async Task UpdateCourseAsync(
        //    string id,
        //    UpdateCourseDto dto)
        //{
        //    var course = await _courseRepo.GetByIdAsync(id);

        //    if (course == null)
        //        throw new NotFoundException(
        //            "Course not found.");

        //    // Update normal course data
        //    _mapper.Map(dto, course);

        //    // Update Thumbnail
        //    if (dto.Thumbnail != null)
        //    {
        //        var oldThumbnailUrl = course.Thumbnail;

        //        course.Thumbnail =
        //            await _fileService.UploadAsync(
        //                dto.Thumbnail,
        //                "thumbnails",
        //                new[] { ".jpg", ".jpeg", ".png", ".webp" },
        //                5 * 1024 * 1024);

        //        if (!string.IsNullOrWhiteSpace(oldThumbnailUrl))
        //        {
        //            await _fileService.DeleteAsync(
        //                oldThumbnailUrl);
        //        }
        //    }

        //    // Update Trailer
        //    if (dto.Trailer != null)
        //    {
        //        var oldTrailerUrl = course.Trailer;

        //        course.Trailer =
        //            await _fileService.UploadAsync(
        //                dto.Trailer,
        //                "trailers",
        //                new[] { ".mp4", ".webm", ".mov" },
        //                100 * 1024 * 1024);

        //        if (!string.IsNullOrWhiteSpace(oldTrailerUrl))
        //        {
        //            await _fileService.DeleteAsync(
        //                oldTrailerUrl);
        //        }
        //    }

        //    course.UpdatedAt = DateTime.UtcNow;

        //    await _courseRepo.UpdateAsync(id, course);
        //}

        public async Task DeleteCourseAsync(string id)
        {
            var course =
                await _courseRepo.GetByIdAsync(id);

            if (course == null)
                throw new NotFoundException(
                    "Course not found.");

            var enrollments =
                await _enrollmentRepo.GetByCourseIdAsync(id);

            if (enrollments.Count > 0)
                throw new BadRequestException(
                    "Course cannot be deleted because students are enrolled in it.");

            await _courseRepo.DeleteAsync(id);
        }

        public async Task PublishCourseAsync(string id)
        {
            var course =
                await _courseRepo.GetByIdAsync(id);

            if (course == null)
                throw new NotFoundException(
                    "Course not found.");

            var lessons =
                await _lessonRepo.GetByCourseIdAsync(
                    course.Id);

            if (lessons.Count == 0)
                throw new BadRequestException(
                    "Course must have at least one lesson before publishing.");

            course.Status = "published";
            course.PublishedAt = DateTime.UtcNow;
            course.UpdatedAt = DateTime.UtcNow;

            await _courseRepo.UpdateAsync(
                id,
                course);
        }
        //public async Task<PaginatedResult<Course>> GetAllAsync(
        //    int page,
        //    int limit)
        //{
        //    if (page < 1)
        //        page = 1;

        //    if (limit < 1)
        //        limit = 10;

        //    var courses = await _courseRepo
        //        .GetAllAsync(page, limit);

        //    var totalItems = await _courseRepo
        //        .CountAsync();

        //    return new PaginatedResult<Course>
        //    {
        //        Items = courses,
        //        Page = page,
        //        Limit = limit,
        //        TotalItems = totalItems
        //    };
        //}
        //public async Task<PaginatedResult<Course>> GetPublishedAsync(
        //    int page,
        //    int limit)
        //{
        //    if (page < 1)
        //        page = 1;

        //    if (limit < 1)
        //        limit = 10;

        //    var courses = await _courseRepo
        //        .GetPublishedAsync(page, limit);

        //    var totalItems = await _courseRepo
        //        .CountPublishedAsync();

        //    return new PaginatedResult<Course>
        //    {
        //        Items = courses,
        //        Page = page,
        //        Limit = limit,
        //        TotalItems = totalItems
        //    };
        //}


    }
 }


