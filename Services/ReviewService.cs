using AutoMapper;
using Educatinal_Platform.DTOs;
using Educatinal_Platform.Exceptions;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using EduPlatformAPI.Repositories;

namespace Educatinal_Platform.Services
{

    public interface IReviewService
    {
        Task<ReviewResponseDto> CreateAsync(
            string studentId,
            string courseId,
            CreateReviewDto dto);

        Task<List<ReviewResponseDto>> GetByCourseIdAsync(
            string courseId);

        Task<ReviewResponseDto?> GetByIdAsync(
            string id);

        Task UpdateAsync(
            string reviewId,
            string studentId,
            UpdateReviewDto dto);

        Task DeleteAsync(
            string reviewId,
            string studentId);
    }


    public class ReviewService : IReviewService
    {
        private readonly IReviewRepository _reviewRepo;
        private readonly IEnrollmentRepository _enrollmentRepo;
        private readonly ICourseRepository _courseRepo;
        private readonly IMapper _mapper;

        public ReviewService(
            IReviewRepository reviewRepo,
            IEnrollmentRepository enrollmentRepo,
            ICourseRepository courseRepo,
            IMapper mapper)
        {
            _reviewRepo = reviewRepo;
            _enrollmentRepo = enrollmentRepo;
            _courseRepo = courseRepo;
            _mapper = mapper;
        }

        public async Task<ReviewResponseDto> CreateAsync(
            string studentId,
            string courseId,
            CreateReviewDto dto)
        {
            // 1. Validate Rating
            if (dto.Rating < 1 || dto.Rating > 5)
                throw new BadRequestException(
                    "Rating must be between 1 and 5.");

            // 2. Validate Comment
            if (string.IsNullOrWhiteSpace(dto.Comment))
                throw new BadRequestException(
                    "Comment is required.");

            var comment = dto.Comment.Trim();

            if (comment.Length > 1000)
                throw new BadRequestException(
                    "Comment cannot exceed 1000 characters.");

            // 3. Course must exist
            var course =
                await _courseRepo.GetByIdAsync(courseId);

            if (course == null)
                throw new NotFoundException(
                    "Course not found.");

            // 4. Student must be enrolled
            var enrollment =
                await _enrollmentRepo
                    .GetByStudentAndCourseAsync(
                        studentId,
                        courseId);

            if (enrollment == null ||
                enrollment.Status == "cancelled")
            {
                throw new BadRequestException(
                    "You must be enrolled in this course to leave a review.");
            }

            // 5. Prevent duplicate review
            var existingReview =
                await _reviewRepo
                    .GetByStudentAndCourseAsync(
                        studentId,
                        courseId);

            if (existingReview != null)
                throw new BadRequestException(
                    "You have already reviewed this course.");

            // 6. Create review
            var review = new Review
            {
                StudentId = studentId,
                CourseId = courseId,
                Rating = dto.Rating,
                Comment = comment,
                CreatedAt = DateTime.UtcNow
            };

            var createdReview =
                await _reviewRepo.CreateAsync(review);

            return _mapper.Map<ReviewResponseDto>(
                createdReview);
        }

        public async Task<List<ReviewResponseDto>>
            GetByCourseIdAsync(string courseId)
        {
            var course =
                await _courseRepo.GetByIdAsync(courseId);

            if (course == null)
                throw new NotFoundException(
                    "Course not found.");

            var reviews =
                await _reviewRepo
                    .GetByCourseIdAsync(courseId);

            return _mapper.Map<
                List<ReviewResponseDto>>(reviews);
        }

        public async Task<ReviewResponseDto?>
            GetByIdAsync(string id)
        {
            var review =
                await _reviewRepo.GetByIdAsync(id);

            if (review == null)
                return null;

            return _mapper.Map<ReviewResponseDto>(
                review);
        }

        public async Task UpdateAsync(
            string reviewId,
            string studentId,
            UpdateReviewDto dto)
        {
            if (dto.Rating < 1 || dto.Rating > 5)
                throw new BadRequestException(
                    "Rating must be between 1 and 5.");

            if (string.IsNullOrWhiteSpace(dto.Comment))
                throw new BadRequestException(
                    "Comment is required.");

            var comment = dto.Comment.Trim();

            if (comment.Length > 1000)
                throw new BadRequestException(
                    "Comment cannot exceed 1000 characters.");

            var review =
                await _reviewRepo.GetByIdAsync(reviewId);

            if (review == null)
                throw new NotFoundException(
                    "Review not found.");

            if (review.StudentId != studentId)
                throw new UnauthorizedAccessException(
                    "You can only update your own review.");

            review.Rating = dto.Rating;
            review.Comment = comment;
            review.UpdatedAt = DateTime.UtcNow;

            await _reviewRepo.UpdateAsync(
                reviewId,
                review);
        }

        public async Task DeleteAsync(
            string reviewId,
            string studentId)
        {
            var review =
                await _reviewRepo.GetByIdAsync(reviewId);

            if (review == null)
                throw new NotFoundException(
                    "Review not found.");

            if (review.StudentId != studentId)
                throw new UnauthorizedAccessException(
                    "You can only delete your own review.");

            await _reviewRepo.DeleteAsync(reviewId);
        }
    }
}