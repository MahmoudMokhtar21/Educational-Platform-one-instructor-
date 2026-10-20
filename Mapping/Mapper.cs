namespace Educatinal_Platform.Mapping
{
    // Mappings/MappingProfile.cs

    using AutoMapper;
    using Educatinal_Platform.DTOs;
    using Educatinal_Platform.Models;

    namespace EduPlatformAPI.Mappings
    {
        public class MappingProfile : Profile
        {
            public MappingProfile()
            {
                // Course → CourseResponseDto
                CreateMap<Course, CourseResponseDto>();

                // Course → CourseDetailDto
                CreateMap<Course, CourseDetailDto>()
                    .ForMember(dest => dest.Lessons,
                        opt => opt.Ignore())
                    .ForMember(dest => dest.Tracks,
                        opt => opt.Ignore())
                    .ForMember(dest => dest.IsEnrolled,
                        opt => opt.Ignore()) // manual
                    // Access is derived from the requesting principal in CourseService,
                    // not persisted on Course.
                    .ForMember(dest => dest.HasAdminAccess,
                        opt => opt.Ignore())
                    .ForMember(dest => dest.ProgressPercent,
                        opt => opt.Ignore())
                    .ForMember(dest => dest.AccessType,
                        opt => opt.Ignore()); // manual

                CreateMap<Course, AdminCourseDto>()
                    .ForMember(dest => dest.Lessons, opt => opt.Ignore())
                    .ForMember(dest => dest.Tracks, opt => opt.Ignore())
                    .ForMember(dest => dest.IsEnrolled, opt => opt.Ignore())
                    // This DTO is produced only for the admin-only course endpoints.
                    .ForMember(dest => dest.HasAdminAccess, opt => opt.MapFrom(_ => true))
                    .ForMember(dest => dest.ProgressPercent, opt => opt.Ignore())
                    .ForMember(dest => dest.AccessType, opt => opt.Ignore());

                // CreateCourseDto → Course
                CreateMap<CreateCourseDto, Course>()
                    .ForMember(dest => dest.Id,
                        opt => opt.Ignore())
                    .ForMember(dest => dest.Slug,
                        opt => opt.MapFrom(src => GenerateSlug(src.Title)))
                    .ForMember(dest => dest.ThumbnailUrl,
                        opt => opt.Ignore())
                    .ForMember(dest => dest.TrailerUrl,
                        opt => opt.Ignore())
                    .ForMember(dest => dest.Status,
                        opt => opt.MapFrom(src => "draft"))
                    .ForMember(dest => dest.CreatedAt,
                        opt => opt.MapFrom(src => DateTime.UtcNow))
                    .ForMember(dest => dest.UpdatedAt,
                        opt => opt.MapFrom(src => DateTime.UtcNow))
                    .ForMember(dest => dest.TotalStudents,
                        opt => opt.MapFrom(src => 0))
                    .ForMember(dest => dest.AverageRating,
                        opt => opt.MapFrom(src => 0m))
                    .ForMember(dest => dest.TotalReviews,
                        opt => opt.MapFrom(src => 0))
                    .ForMember(dest => dest.Category,
                        opt => opt.Ignore())
                    .ForMember(dest => dest.Categories,
                        opt => opt.Ignore())
                    .ForMember(dest => dest.TotalHours,
                        opt => opt.MapFrom(src => 0m))
                    .ForMember(dest => dest.TotalLessons,
                        opt => opt.MapFrom(src => 0))
                    .ForMember(dest => dest.PublishedAt,
                        opt => opt.Ignore());

                // UpdateCourseDto → Course
                CreateMap<UpdateCourseDto, Course>(MemberList.None)
                    .ForMember(dest => dest.ThumbnailUrl,
                        opt => opt.Ignore())
                    .ForMember(dest => dest.TrailerUrl,
                        opt => opt.Ignore())
                    .ForAllMembers(opts => opts.Condition((src, dest, srcMember) =>
                        srcMember != null));


                // ===== Lesson Mappings =====
                CreateMap<Lesson, LessonDto>()
                    .ForMember(dest => dest.VideoUrl, opt => opt.Ignore())
                    .ForMember(dest => dest.HasVideo, opt => opt.MapFrom(src => !string.IsNullOrWhiteSpace(src.VideoUrl)))
                    .ForMember(dest => dest.IsWatched, opt => opt.Ignore())
                    .ForMember(dest => dest.IsCompleted, opt => opt.Ignore())
                    .ForMember(dest => dest.IsLocked, opt => opt.Ignore())
                    .ForMember(dest => dest.IsCurrent, opt => opt.Ignore());
                CreateMap<LessonResource, LessonResourceDto>()
                    .ForMember(dest => dest.FileUrl, opt => opt.Ignore());

                // Category Mapping
                CreateMap<Category, CategoryResponseDto>();
                CreateMap<CategoryEmbedded, CategoryEmbeddedDto>();

                // Enrollment Mapping
                CreateMap<Enrollment, EnrollmentDto>();
                CreateMap<WatchedLesson, WatchedLessonDto>();

                // Review Mapping
                CreateMap<Review, ReviewResponseDto>();


            }

            private string GenerateSlug(string title)
            {
                if (string.IsNullOrEmpty(title))
                    return string.Empty;

                return title.ToLower()
                    .Replace(".", "")
                    .Replace(" ", "-")
                    .Replace("--", "-")
                    .Replace("'", "")
                    .Replace("\"", "");
            }
        }
    }
}
