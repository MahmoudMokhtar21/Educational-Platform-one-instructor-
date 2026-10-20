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
            Task<PagedResult<CourseResponseDto>> GetPublishedCoursesAsync(int page, int limit, string? search = null, string? category = null, string? level = null, string? sort = null);
            Task<PagedResult<AdminCourseDto>> GetAdminCoursesAsync(int page, int limit, string? search = null, string? category = null, string? level = null, string? sort = null);
            Task<AdminCourseDto?> GetAdminCourseAsync(string id);
            Task<CourseDetailDto?> GetCourseDetailAsync(string slug, string? studentId, bool isAdmin = false);
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
        private readonly ICategoryRepository _categoryRepo;
        private readonly ITrackService _trackService;
        private readonly ICourseLessonAccessService _courseLessonAccessService;

        public CourseService(
            ICourseRepository courseRepo,
            IEnrollmentRepository enrollmentRepo,
            IMapper mapper,
            ILessonRepository lessonRepo,
            IFileService fileService,
            ICategoryRepository categoryRepo,
            ITrackService trackService,
            ICourseLessonAccessService courseLessonAccessService
           )
        {
            _courseRepo = courseRepo;
            _enrollmentRepo = enrollmentRepo;
            _mapper = mapper;
            _lessonRepo = lessonRepo;
            _fileService = fileService;
            _categoryRepo = categoryRepo;
            _trackService = trackService;
            _courseLessonAccessService = courseLessonAccessService;
        }

        public async Task<PagedResult<CourseResponseDto>> GetPublishedCoursesAsync(int page, int limit, string? search = null, string? category = null, string? level = null, string? sort = null)
        {

            if (page < 1)
                throw new BadRequestException("Page number must be greater than zero.");

            if (limit < 1 || limit > 100)
                throw new BadRequestException("Limit must be between 1 and 100.");
            if (search?.Length > 100)
                throw new BadRequestException("Search text must be 100 characters or fewer.");

            var courses = await _courseRepo.GetPublishedAsync(page, limit, search, category, level, sort);
            var totalItems = await _courseRepo.CountPublishedAsync(search, category, level);

            foreach (var course in courses)
                NormalizeCourseCategories(course);

            var dtos = _mapper.Map<List<CourseResponseDto>>(courses);
            for (var index = 0; index < courses.Count && index < dtos.Count; index++)
                ApplyCoursePricing(dtos[index], courses[index]);

            return new PagedResult<CourseResponseDto>
            {
                Items = dtos,
                TotalItems = (int)totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)limit),
                CurrentPage = page,
                PageSize = limit
            };
        }

        public async Task<PagedResult<AdminCourseDto>> GetAdminCoursesAsync(int page, int limit, string? search = null, string? category = null, string? level = null, string? sort = null)
        {
            if (page < 1) throw new BadRequestException("Page number must be greater than zero.");
            if (limit < 1 || limit > 100) throw new BadRequestException("Limit must be between 1 and 100.");
            if (search?.Length > 100) throw new BadRequestException("Search text must be 100 characters or fewer.");
            var courses = await _courseRepo.GetAllAsync(page, limit, search, category, level, sort);
            var totalItems = await _courseRepo.CountAsync(search, category, level);

            foreach (var course in courses)
                NormalizeCourseCategories(course);

            var dtos = _mapper.Map<List<AdminCourseDto>>(courses);
            for (var index = 0; index < courses.Count && index < dtos.Count; index++)
                ApplyCoursePricing(dtos[index], courses[index]);

            return new PagedResult<AdminCourseDto>
            {
                Items = dtos,
                TotalItems = (int)totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)limit),
                CurrentPage = page,
                PageSize = limit
            };
        }

        public async Task<AdminCourseDto?> GetAdminCourseAsync(string id)
        {
            var course = await _courseRepo.GetByIdAsync(id);
            if (course == null) return null;

            NormalizeCourseCategories(course);

            var dto = _mapper.Map<AdminCourseDto>(course);
            ApplyCoursePricing(dto, course);
            return dto;
        }

        public async Task<CourseDetailDto?> GetCourseDetailAsync(string slug, string? studentId, bool isAdmin = false)
        {
            if (string.IsNullOrWhiteSpace(slug))
                throw new BadRequestException("Course slug is required.");

            var course = await _courseRepo.GetBySlugAsync(slug);
            if (course == null)
                throw new NotFoundException("Course not found.");
            if (course.Status != "published")
                throw new BadRequestException("Course is not published yet.");

            NormalizeCourseCategories(course);

            var dto = _mapper.Map<CourseDetailDto>(course);
            ApplyCoursePricing(dto, course);
            dto.HasAdminAccess = isAdmin;

            var tracks = await _trackService.GetByCourseIdAsync(course.Id);
            var lessons = await _courseLessonAccessService.GetOrderedLessonsAsync(course.Id);
            dto.Lessons = _mapper.Map<List<LessonDto>>(lessons);

            // Platform-hosted resource paths remain private. Only external HTTP(S)
            // resource links are retained in the course DTO. Protected streaming is
            // still authorized again when a URL is requested or served.
            for (var lessonIndex = 0; lessonIndex < lessons.Count && lessonIndex < dto.Lessons.Count; lessonIndex++)
            {
                var sourceLesson = lessons[lessonIndex];
                var mappedLesson = dto.Lessons[lessonIndex];
                for (var resourceIndex = 0; resourceIndex < sourceLesson.Resources.Count && resourceIndex < mappedLesson.Resources.Count; resourceIndex++)
                {
                    var sourceResource = sourceLesson.Resources[resourceIndex];
                    if (IsSafeExternalResourceUrl(sourceResource.FileUrl))
                        mappedLesson.Resources[resourceIndex].FileUrl = sourceResource.FileUrl;
                }
            }

            Enrollment? enrollment = null;
            if (!string.IsNullOrWhiteSpace(studentId))
            {
                enrollment = await _enrollmentRepo.GetByStudentAndCourseAsync(studentId, course.Id);
                if (enrollment != null &&
                    (enrollment.Status == "active" || enrollment.Status == "completed"))
                {
                    dto.IsEnrolled = true;
                    dto.ProgressPercent = enrollment.ProgressPercent;
                    dto.AccessType = CourseAccessTypes.Resolve(enrollment.AccessType);

                    var watchedLessons = enrollment.WatchedLessons.ToDictionary(item => item.LessonId);
                    foreach (var lesson in dto.Lessons)
                    {
                        if (watchedLessons.TryGetValue(lesson.Id, out var watched))
                        {
                            lesson.IsWatched = true;
                            lesson.IsCompleted = watched.IsCompleted;
                        }
                    }

                    var accessStates = await _courseLessonAccessService.GetAccessStatesAsync(enrollment, lessons);
                    foreach (var lesson in dto.Lessons)
                    {
                        if (!accessStates.TryGetValue(lesson.Id, out var access))
                            continue;

                        lesson.IsLocked = access.IsLocked;
                        lesson.IsCurrent = access.IsCurrent;
                        // A locked sequential lesson exposes only its syllabus
                        // metadata. Preview video behavior is retained unchanged.
                        if (access.IsLocked)
                        {
                            lesson.ContentText = null;
                            lesson.Resources = new List<LessonResourceDto>();
                            if (!lesson.IsPreview)
                            {
                                lesson.VideoUrl = null;
                                lesson.HasVideo = false;
                            }
                        }
                    }
                }
            }

            // Public course pages may show a syllabus. Its protected material is
            // unavailable until enrollment, except the pre-existing preview video.
            if (!dto.IsEnrolled && !dto.HasAdminAccess)
            {
                foreach (var lesson in dto.Lessons)
                {
                    lesson.ContentText = null;
                    lesson.Resources = new List<LessonResourceDto>();
                    if (!lesson.IsPreview)
                    {
                        lesson.VideoUrl = null;
                        lesson.HasVideo = false;
                    }
                }
            }

            dto.Tracks = tracks
                .OrderBy(track => track.OrderIndex)
                .Select(track => new TrackDto
                {
                    Id = track.Id,
                    CourseId = track.CourseId,
                    Title = track.Title,
                    Description = track.Description,
                    OrderIndex = track.OrderIndex,
                    CreatedAt = track.CreatedAt,
                    Lessons = dto.Lessons
                        .Where(lesson => string.Equals(lesson.TrackId, track.Id, StringComparison.Ordinal))
                        .OrderBy(lesson => lesson.OrderIndex)
                        .ToList()
                })
                .ToList();

            return dto;
        }

        public async Task<Course> CreateCourseAsync(CreateCourseDto dto)
        {
            var course = _mapper.Map<Course>(dto);
            course.ShortDescription = NormalizeShortDescription(dto.ShortDescription);
            ApplyCoursePricing(course, dto.BasicPrice ?? dto.Price, dto.FullAccessPrice);

            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new BadRequestException(
                    "Course title is required.");

            var requestedCategoryIds = GetRequestedCategoryIds(dto.CategoryIds, dto.CategoryId);
            if (requestedCategoryIds.Count > 0)
                await AssignCategoriesAsync(course, requestedCategoryIds, preserveExistingUnavailable: false);

            var originalSlug = course.Slug;
            var slug = originalSlug;
            var counter = 1;

            while (await _courseRepo.ExistsAsync(slug))
            {
                slug = $"{originalSlug}-{counter}";
                counter++;
            }

            course.Slug = slug;
            string? uploadedThumbnailUrl = null;
            string? uploadedTrailerUrl = null;

            try
            {
                if (dto.Thumbnail != null)
                {
                    uploadedThumbnailUrl = await _fileService.UploadImageAsync(
                        dto.Thumbnail,
                        "thumbnails");
                    course.ThumbnailUrl = uploadedThumbnailUrl;
                }

                if (dto.Trailer != null)
                {
                    uploadedTrailerUrl = await _fileService.UploadAsync(
                        dto.Trailer,
                        "trailers",
                        new[] { ".mp4", ".webm", ".mov" },
                        100 * 1024 * 1024);
                    course.TrailerUrl = uploadedTrailerUrl;
                }

                course.Status = "draft";
                course.CreatedAt = DateTime.UtcNow;
                course.UpdatedAt = DateTime.UtcNow;

                return await _courseRepo.CreateAsync(course);
            }
            catch
            {
                await DeleteFileQuietlyAsync(uploadedThumbnailUrl);
                await DeleteFileQuietlyAsync(uploadedTrailerUrl);
                throw;
            }
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
            if (dto.ShortDescription != null)
                course.ShortDescription = NormalizeShortDescription(dto.ShortDescription);

            ApplyCoursePricing(course, dto.BasicPrice ?? dto.Price, dto.FullAccessPrice ?? course.FullAccessPrice);
            course.Level = dto.Level;

            if (dto.ReplaceCategories == true || dto.CategoryIds != null || !string.IsNullOrWhiteSpace(dto.CategoryId))
            {
                var requestedCategoryIds = GetRequestedCategoryIds(dto.CategoryIds, dto.CategoryId);
                await AssignCategoriesAsync(course, requestedCategoryIds, preserveExistingUnavailable: true);
            }

            if (dto.Status != null)
                course.Status = dto.Status;

            if (dto.Tags != null)
                course.Tags = dto.Tags;

            var oldThumbnailUrl = course.ThumbnailUrl;
            var oldTrailerUrl = course.TrailerUrl;
            string? uploadedThumbnailUrl = null;
            string? uploadedTrailerUrl = null;

            try
            {
                if (dto.Thumbnail != null)
                {
                    uploadedThumbnailUrl = await _fileService.UploadImageAsync(
                        dto.Thumbnail,
                        "thumbnails");
                }

                if (dto.Trailer != null)
                {
                    uploadedTrailerUrl = await _fileService.UploadAsync(
                        dto.Trailer,
                        "trailers",
                        new[] { ".mp4", ".webm", ".mov" },
                        100 * 1024 * 1024);
                }

                if (uploadedThumbnailUrl != null)
                    course.ThumbnailUrl = uploadedThumbnailUrl;
                if (uploadedTrailerUrl != null)
                    course.TrailerUrl = uploadedTrailerUrl;

                course.UpdatedAt = DateTime.UtcNow;
                await _courseRepo.UpdateAsync(id, course);
            }
            catch
            {
                await DeleteFileQuietlyAsync(uploadedThumbnailUrl);
                await DeleteFileQuietlyAsync(uploadedTrailerUrl);
                throw;
            }

            if (uploadedThumbnailUrl != null &&
                !string.Equals(oldThumbnailUrl, uploadedThumbnailUrl, StringComparison.Ordinal))
            {
                await DeleteFileQuietlyAsync(oldThumbnailUrl);
            }

            if (uploadedTrailerUrl != null &&
                !string.Equals(oldTrailerUrl, uploadedTrailerUrl, StringComparison.Ordinal))
            {
                await DeleteFileQuietlyAsync(oldTrailerUrl);
            }
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

        private async Task DeleteFileQuietlyAsync(string? fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return;

            try
            {
                await _fileService.DeleteAsync(fileUrl);
            }
            catch
            {
                // The database update is already committed. A cleanup failure must not
                // make the successful course update appear to have failed.
            }
        }

        private async Task AssignCategoriesAsync(
            Course course,
            IReadOnlyCollection<string> categoryIds,
            bool preserveExistingUnavailable)
        {
            NormalizeCourseCategories(course);
            var existingById = course.Categories
                .Where(category => !string.IsNullOrWhiteSpace(category.Id))
                .GroupBy(category => category.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            var selected = new List<CategoryEmbedded>();
            foreach (var categoryId in categoryIds)
            {
                var category = await _categoryRepo.GetByIdAsync(categoryId);
                if (category == null)
                {
                    // A deleted category can be preserved only while an administrator
                    // keeps an existing assignment unchanged. It can still be removed.
                    if (preserveExistingUnavailable && existingById.TryGetValue(categoryId, out var unavailableCategory))
                    {
                        selected.Add(unavailableCategory);
                        continue;
                    }

                    throw new BadRequestException("Category not found.");
                }

                // New assignments must remain limited to categories that are currently
                // available. An already-assigned inactive category is preserved so that
                // a routine course edit does not silently remove legacy data.
                if (!category.IsActive &&
                    (!preserveExistingUnavailable || !existingById.ContainsKey(category.Id)))
                {
                    throw new BadRequestException("Selected category is not active.");
                }

                selected.Add(new CategoryEmbedded
                {
                    Id = category.Id,
                    Name = category.Name,
                    Slug = category.Slug
                });
            }

            course.Categories = selected;
            course.Category = selected.FirstOrDefault();
        }

        private static List<string> GetRequestedCategoryIds(
            IEnumerable<string>? categoryIds,
            string? legacyCategoryId)
        {
            var ids = categoryIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList()
                ?? new List<string>();

            if (ids.Count == 0 && !string.IsNullOrWhiteSpace(legacyCategoryId))
                ids.Add(legacyCategoryId.Trim());

            return ids;
        }

        private static void NormalizeCourseCategories(Course course)
        {
            var categories = new List<CategoryEmbedded>();

            void Add(CategoryEmbedded? category)
            {
                if (category == null || string.IsNullOrWhiteSpace(category.Id))
                    return;

                if (categories.Any(item => string.Equals(item.Id, category.Id, StringComparison.Ordinal)))
                    return;

                categories.Add(new CategoryEmbedded
                {
                    Id = category.Id,
                    Name = category.Name,
                    Slug = category.Slug
                });
            }

            // Put the legacy primary category first so old clients retain their
            // original category assignment while new clients see every category.
            Add(course.Category);
            foreach (var category in course.Categories ?? new List<CategoryEmbedded>())
                Add(category);

            course.Categories = categories;
            course.Category = categories.FirstOrDefault();
        }

        private static string? NormalizeShortDescription(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static void ApplyCoursePricing(CourseResponseDto dto, Course course)
        {
            dto.BasicPrice = CourseAccessTypes.BasicPrice(course);
            dto.FullAccessPrice = CourseAccessTypes.FullAccessPrice(course);
            // Price stays a stable alias for older clients and catalog views.
            dto.Price = dto.BasicPrice;
        }

        private static void ApplyCoursePricing(Course course, decimal basicPrice, decimal? fullAccessPrice)
        {
            var resolvedFullAccessPrice = fullAccessPrice ?? basicPrice;
            if (basicPrice < 0 || resolvedFullAccessPrice < 0)
                throw new BadRequestException("Course prices cannot be negative.");
            if (resolvedFullAccessPrice < basicPrice)
                throw new BadRequestException("Full access price must be greater than or equal to the sequential price.");

            course.Price = basicPrice;
            course.BasicPrice = basicPrice;
            course.FullAccessPrice = resolvedFullAccessPrice;
        }

        private static bool IsSafeExternalResourceUrl(string? value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

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


