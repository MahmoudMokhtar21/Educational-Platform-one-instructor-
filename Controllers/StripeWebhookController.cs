using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using EduPlatformAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Educatinal_Platform.Controllers
{
    [ApiController]
    [Route("api/stripe")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IEnrollmentRepository _enrollmentRepository;
        private readonly ICourseRepository _courseRepository;

        public StripeWebhookController(
            IConfiguration configuration,
            IEnrollmentRepository enrollmentRepository,
            ICourseRepository courseRepository)
        {
            _configuration = configuration;
            _enrollmentRepository = enrollmentRepository;
            _courseRepository = courseRepository;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(
                HttpContext.Request.Body)
                .ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(json))
            {
                return BadRequest(new
                {
                    message = "Webhook body is empty."
                });
            }
            var webhookSecret =
                _configuration["Stripe:WebhookSecret"];

            var signature =
                Request.Headers["Stripe-Signature"].ToString();

            if (string.IsNullOrWhiteSpace(signature))
            {
                return BadRequest(new
                {
                    message = "Stripe-Signature header is missing."
                });
            }
            if (string.IsNullOrWhiteSpace(webhookSecret))
            {
                return BadRequest(new
                {
                    message = "Stripe Webhook Secret is not configured."
                });
            }
            
            try
            {
                var stripeEvent =
                    EventUtility.ConstructEvent(
                    json,
                    signature,
                    webhookSecret);

                if (stripeEvent.Type == "payment_intent.succeeded")
                {
                    var paymentIntent =
                        stripeEvent.Data.Object as PaymentIntent;

                    if (paymentIntent == null)
                        return BadRequest();

                    if (!paymentIntent.Metadata.TryGetValue("StudentId", out var studentId) ||
                        !paymentIntent.Metadata.TryGetValue("CourseId", out var courseId))
                    {
                        return BadRequest(new
                        {
                            message = "StudentId or CourseId metadata is missing."
                        });
                    }
                    var existingEnrollment =
                        await _enrollmentRepository
                        .GetByStudentAndCourseAsync(studentId, courseId);

                    if (existingEnrollment != null)
                    {
                        return Ok();
                    }
                    var course =
                        await _courseRepository.GetByIdAsync(courseId);

                    if (course == null)
                    {
                        return BadRequest(new
                        {
                            message = "Course not found."
                        });
                    }
                    var enrollment = new Enrollment
                    {
                        StudentId = studentId,
                        CourseId = courseId,
                        EnrolledAt = DateTime.UtcNow,
                        Status = "active",
                        ProgressPercent = 0
                    };

                    await _enrollmentRepository.CreateAsync(enrollment);
                    await _courseRepository.UpdateStatsAsync(
                        courseId,
                        studentDelta: 1,
                        ratingDelta: 0);
                    
                }

                return Ok();
            }
            catch (StripeException)
            {
                return BadRequest();
            }
        }
    }
}