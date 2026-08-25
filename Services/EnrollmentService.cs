using AutoMapper;
using Educatinal_Platform.DTOs;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using EduPlatformAPI.Repositories;
namespace Educatinal_Platform.Services
{
    public interface IEnrollmentService
    {
        Task<EnrollmentResult> EnrollAsync(
           string studentId,
           string courseId);

        Task<EnrollmentDto?> GetMyEnrollmentAsync(
            string studentId,
            string courseId);

        Task UpdateLessonProgressAsync(
            string studentId,
            string courseId,
            string lessonId,
            int watchDurationSeconds,
            bool isCompleted);
        Task<MyCoursesResponseDto> GetMyCoursesAsync(
            string studentId);
        Task CancelEnrollmentAsync(
            string studentId,
            string courseId);

    }

    public class EnrollmentService : IEnrollmentService
    {
        private readonly IEnrollmentRepository _enrollmentRepo;
        private readonly ICourseRepository _courseRepo;
        private readonly ILessonRepository _lessonRepo;
        private readonly IPaymentService _paymentService;
        private readonly IMapper _mapper;

        public EnrollmentService(
            IEnrollmentRepository enrollmentRepo,
            ICourseRepository courseRepo,
            ILessonRepository lessonRepo,
            IPaymentService paymentService,
            IMapper mapper)
        {
            _enrollmentRepo = enrollmentRepo;
            _courseRepo = courseRepo;
            _lessonRepo = lessonRepo;
            _paymentService = paymentService;
            _mapper = mapper;
        }

        public async Task<EnrollmentResult> EnrollAsync(
            string studentId,
            string courseId)
        {
            var course =
                await _courseRepo.GetByIdAsync(courseId);

            if (course == null)
            {
                return new EnrollmentResult
                {
                    Success = false,
                    Message = "Course not found"
                };
            }

            if (course.Status != "published")
            {
                return new EnrollmentResult
                {
                    Success = false,
                    Message = "Course is not available"
                };
            }

            var existingEnrollment =
                await _enrollmentRepo
                .GetByStudentAndCourseAsync(
                studentId,
                courseId);

            if (existingEnrollment != null)
            {
                if (existingEnrollment.Status == "active")
                {
                    return new EnrollmentResult
                    {
                        Success = false,
                        Message = "Already enrolled"
                    };
                }

                if (existingEnrollment.Status == "completed")
                {
                    return new EnrollmentResult
                    {
                        Success = false,
                        Message = "Course already completed"
                    };
                }

            }

            if (course.Price > 0)
            {
                var payment =
                    await _paymentService.CreatePaymentIntentAsync(
                        course.Price,
                        "usd",
                        studentId,
                        courseId);

                return new EnrollmentResult
                {
                    Success = true,
                    RequiresPayment = true,
                    PaymentIntentId =
                        payment.PaymentIntentId,
                    ClientSecret =
                        payment.ClientSecret,
                    Message = "Payment required"
                };
            }

            var enrollment = new Enrollment
            {
                StudentId = studentId,
                CourseId = courseId,
                EnrolledAt = DateTime.UtcNow,
                Status = "active",
                ProgressPercent = 0,
                WatchedLessons = new List<WatchedLesson>()
            };

            await _enrollmentRepo.CreateAsync(enrollment);

            await _courseRepo.UpdateStatsAsync(
                courseId,
                1,
                0);

            return new EnrollmentResult
            {
                Success = true,
                RequiresPayment = false,
                EnrollmentId = enrollment.Id,
                Message = "Enrolled successfully"
            };
        }

        public async Task<EnrollmentDto?>
            GetMyEnrollmentAsync(
                string studentId,
                string courseId)
        {
            var enrollment =
                await _enrollmentRepo
                    .GetByStudentAndCourseAsync(
                        studentId,
                        courseId);

            if (enrollment == null)
                return null;

            return _mapper.Map<EnrollmentDto>(
                enrollment);
        }

        public async Task UpdateLessonProgressAsync(
            string studentId,
            string courseId,
            string lessonId,
            int watchDurationSeconds,
            bool isCompleted)
        {
            var enrollment =
                await _enrollmentRepo
                    .GetActiveByStudentAndCourseAsync(
                        studentId,
                        courseId);

            if (enrollment == null)
                throw new KeyNotFoundException(
                    "Enrollment not found.");

            if (enrollment.Status != "active")
                throw new InvalidOperationException(
                    "Enrollment is not active.");

            var lesson =
                await _lessonRepo.GetByIdAsync(
                    lessonId);

            if (lesson == null)
                throw new KeyNotFoundException(
                    "Lesson not found.");

            if (lesson.CourseId != courseId)
                throw new ArgumentException(
                    "Lesson does not belong to this course.");

            var watchedLesson = new WatchedLesson
            {
                LessonId = lesson.Id,
                LessonTitle = lesson.Title,
                WatchedAt = DateTime.UtcNow,
                WatchDurationSeconds =
                    watchDurationSeconds,
                IsCompleted = isCompleted
            };

            
            var lessons =
                await _lessonRepo
                    .GetByCourseIdAsync(courseId);

            var updatedWatchedLessons =
                enrollment.WatchedLessons
                    .Where(w => w.LessonId != lessonId)
                    .ToList();

            updatedWatchedLessons.Add(
                watchedLesson);

            var completedLessons =
                updatedWatchedLessons
                .Count(w =>w.IsCompleted &&
                lessons.Any(l => l.Id == w.LessonId));

            decimal progress = 0;

            if (lessons.Count > 0)
            {
                progress =
                    (decimal)completedLessons /
                    lessons.Count * 100;
            }

            if (progress > 100)
                progress = 100;

            enrollment.WatchedLessons =
                updatedWatchedLessons;

            enrollment.ProgressPercent =
                progress;

            enrollment.LastWatchedLessonId =
                lessonId;

            enrollment.LastWatchedAt =
                DateTime.UtcNow;

            if (lessons.Count > 0 &&
                completedLessons == lessons.Count)
            {
                enrollment.ProgressPercent = 100;
                enrollment.Status = "completed";    

                if (!enrollment.CompletedAt.HasValue)
                {
                    enrollment.CompletedAt =
                        DateTime.UtcNow;
                }
            }
            else
            {
                enrollment.Status = "active";
                enrollment.CompletedAt = null;
            }

            await _enrollmentRepo.UpdateAsync(
                enrollment.Id,
                enrollment);
        }

        public async Task<MyCoursesResponseDto> GetMyCoursesAsync(
            string studentId)
        {
            var enrollments =
                await _enrollmentRepo.GetByStudentIdAsync(studentId);

            var activeCourses = new List<EnrollmentResponseDto>();
            var completedCourses = new List<EnrollmentResponseDto>();

            foreach (var enrollment in enrollments)
            {
                var course =
                    await _courseRepo.GetByIdAsync(
                        enrollment.CourseId);

                if (course == null)
                    continue;

                var lessons =
                    await _lessonRepo.GetByCourseIdAsync(
                        enrollment.CourseId);

                var completedLessons =
                    enrollment.WatchedLessons
                        .Count(w =>
                        w.IsCompleted &&
                        lessons.Any(l => l.Id == w.LessonId));

                var dto = new EnrollmentResponseDto
                {
                    Id = enrollment.Id,
                    StudentId = enrollment.StudentId,
                    CourseId = enrollment.CourseId,

                    CourseTitle = course.Title,
                    CourseSlug = course.Slug,
                    CourseThumbnail = course.ThumbnailUrl,
                    CoursePrice = course.Price,

                    EnrolledAt = enrollment.EnrolledAt,
                    CompletedAt = enrollment.CompletedAt,
                    ProgressPercent = enrollment.ProgressPercent,
                    Status = enrollment.Status,

                    TotalLessons = lessons.Count,
                    CompletedLessons = completedLessons,

                    FinalExamScore =
                        enrollment.FinalExamScorePercent,

                    HasCertificate =
                        enrollment.IsCertificateIssued
                };

                if (enrollment.Status == "completed")
                {
                    completedCourses.Add(dto);
                }
                else if (enrollment.Status == "active")
                {
                    activeCourses.Add(dto);
                }
            }

            decimal totalProgress = 0;

            if (activeCourses.Count > 0)
            {
                totalProgress =
                    activeCourses.Average(
                        c => c.ProgressPercent);
            }

            return new MyCoursesResponseDto
            {
                ActiveCourses = activeCourses,
                CompletedCourses = completedCourses,

                TotalActive = activeCourses.Count,
                TotalCompleted = completedCourses.Count,

                TotalProgress = totalProgress
            };
        }
        public async Task CancelEnrollmentAsync(
            string studentId,
            string courseId)
        {
            var enrollment =
                await _enrollmentRepo
                    .GetByStudentAndCourseAsync(
                        studentId,
                        courseId);

            if (enrollment == null)
                throw new KeyNotFoundException(
                    "Enrollment not found.");

            if (enrollment.Status == "completed")
                throw new InvalidOperationException(
                    "Completed enrollment cannot be cancelled.");

            if (enrollment.Status == "cancelled")
                throw new InvalidOperationException(
                    "Enrollment is already cancelled.");

            if (enrollment.ProgressPercent > 30)
                throw new InvalidOperationException(
                    "Enrollment cannot be cancelled after more than 30% progress.");
                
            enrollment.Status = "cancelled";

            await _enrollmentRepo.UpdateAsync(
                enrollment.Id,
                enrollment);

            await _courseRepo.UpdateStatsAsync(
                courseId,
                -1,
                0);
        }
    }
}
