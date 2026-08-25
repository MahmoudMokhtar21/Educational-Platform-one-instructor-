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
                    .ForMember(dest => dest.IsEnrolled,
                        opt => opt.Ignore()) // manual
                    .ForMember(dest => dest.ProgressPercent,
                        opt => opt.Ignore()); // manual

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
                        opt => opt.MapFrom(src => 0));

                // UpdateCourseDto → Course
                CreateMap<UpdateCourseDto, Course>()
                    .ForMember(dest => dest.ThumbnailUrl,
                        opt => opt.Ignore())
                    .ForMember(dest => dest.TrailerUrl,
                        opt => opt.Ignore())
                    .ForAllMembers(opts => opts.Condition((src, dest, srcMember) =>
                        srcMember != null));


                // ===== Lesson Mappings =====
                CreateMap<Lesson, LessonDto>()
                    .ForMember(dest => dest.IsWatched, opt => opt.Ignore())
                    .ForMember(dest => dest.IsCompleted, opt => opt.Ignore());

                // Category Mapping
                CreateMap<Category, CategoryResponseDto>();

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
