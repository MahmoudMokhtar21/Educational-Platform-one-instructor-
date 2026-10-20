using AutoMapper;
using Educatinal_Platform.DTOs;
using Educatinal_Platform.Exceptions;
using Educatinal_Platform.Options;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using EduPlatformAPI.Repositories;
using Microsoft.Extensions.Options;
namespace Educatinal_Platform.Services
{
    public interface IEnrollmentService
    {
        Task<EnrollmentResult> EnrollAsync(
           string studentId,
           string courseId,
           PaymentCustomer customer,
           string? requestedAccessType,
           CancellationToken cancellationToken = default);

        Task<PaymentCompletionResult> ConfirmMockPaymentAsync(
            string studentId,
            string paymentReference,
            CancellationToken cancellationToken = default);

        Task<PaymentCompletionResult?> ConfirmPaymobPaymentAsync(
            PaymobTransactionCallback callback,
            CancellationToken cancellationToken = default);

        Task<PaymentCompletionResult?> ConfirmKashierPaymentAsync(
            KashierTransactionCallback callback,
            CancellationToken cancellationToken = default);

        Task<EnrollmentDto?> GetMyEnrollmentAsync(
            string studentId,
            string courseId);

        Task<LessonProgressUpdateResult> UpdateLessonProgressAsync(
            string studentId,
            string courseId,
            string lessonId,
            int watchDurationSeconds);
        Task<MyCoursesResponseDto> GetMyCoursesAsync(
            string studentId);
        Task CancelEnrollmentAsync(
            string studentId,
            string courseId);
        Task<AccessCodeEnrollmentResult> GrantSequentialAccessCodeEnrollmentAsync(
            string studentId,
            string courseId,
            string courseAccessCodeId,
            CancellationToken cancellationToken = default);

        Task<PaymentStatusResult> GetPaymentStatusAsync(
            string studentId,
            string paymentReference,
            CancellationToken cancellationToken = default);

    }

    public class EnrollmentService : IEnrollmentService
    {
        private readonly IEnrollmentRepository _enrollmentRepo;
        private readonly ICourseRepository _courseRepo;
        private readonly ILessonRepository _lessonRepo;
        private readonly IPaymentService _paymentService;
        private readonly IPaymentTransactionRepository _paymentTransactionRepo;
        private readonly ICourseLessonAccessService _courseLessonAccessService;
        private readonly LessonProgressOptions _lessonProgressOptions;
        private readonly IMapper _mapper;
        private readonly ILogger<EnrollmentService> _logger;

        public EnrollmentService(
            IEnrollmentRepository enrollmentRepo,
            ICourseRepository courseRepo,
            ILessonRepository lessonRepo,
            IPaymentService paymentService,
            IPaymentTransactionRepository paymentTransactionRepo,
            ICourseLessonAccessService courseLessonAccessService,
            IOptions<LessonProgressOptions> lessonProgressOptions,
            IMapper mapper,
            ILogger<EnrollmentService> logger)
        {
            _enrollmentRepo = enrollmentRepo;
            _courseRepo = courseRepo;
            _lessonRepo = lessonRepo;
            _paymentService = paymentService;
            _paymentTransactionRepo = paymentTransactionRepo;
            _courseLessonAccessService = courseLessonAccessService;
            _lessonProgressOptions = lessonProgressOptions.Value;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<EnrollmentResult> EnrollAsync(
            string studentId,
            string courseId,
            PaymentCustomer customer,
            string? requestedAccessType,
            CancellationToken cancellationToken = default)
        {
            var course = await _courseRepo.GetByIdAsync(courseId);
            if (course == null)
                return new EnrollmentResult { Success = false, Message = "Course not found" };

            if (course.Status != "published")
                return new EnrollmentResult { Success = false, Message = "Course is not available" };

            string accessType;
            try
            {
                // Omitted input remains Full only for API callers that predate plans.
                // The new checkout UI always sends an explicit, validated plan.
                accessType = CourseAccessTypes.RequireKnown(requestedAccessType);
            }
            catch (ArgumentException)
            {
                return new EnrollmentResult
                {
                    Success = false,
                    ErrorCode = "access_type_invalid",
                    Message = "The selected course access plan is invalid."
                };
            }

            var selectedPrice = CourseAccessTypes.PriceFor(course, accessType);
            if (selectedPrice < 0)
            {
                return new EnrollmentResult
                {
                    Success = false,
                    ErrorCode = "course_price_invalid",
                    Message = "The selected course price is invalid."
                };
            }

            var existingEnrollment = await _enrollmentRepo.GetByStudentAndCourseAsync(studentId, courseId);
            if (existingEnrollment != null)
            {
                if (existingEnrollment.Status == "active")
                    return new EnrollmentResult { Success = false, Message = "Already enrolled" };
                if (existingEnrollment.Status == "completed")
                    return new EnrollmentResult { Success = false, Message = "Course already completed" };
            }

            if (selectedPrice > 0)
            {
                return await CreatePaidCheckoutAsync(
                    studentId,
                    course,
                    selectedPrice,
                    accessType,
                    customer,
                    cancellationToken);
            }

            var enrollment = new Enrollment
            {
                StudentId = studentId,
                CourseId = courseId,
                AccessType = accessType,
                EnrolledAt = DateTime.UtcNow,
                Status = "active",
                ProgressPercent = 0,
                WatchedLessons = new List<WatchedLesson>()
            };

            await _enrollmentRepo.CreateAsync(enrollment);
            await _courseRepo.UpdateStatsAsync(courseId, 1, 0);

            return new EnrollmentResult
            {
                Success = true,
                RequiresPayment = false,
                EnrollmentId = enrollment.Id,
                AccessType = enrollment.AccessType,
                Message = "Enrolled successfully"
            };
        }

        public async Task<AccessCodeEnrollmentResult> GrantSequentialAccessCodeEnrollmentAsync(
            string studentId,
            string courseId,
            string courseAccessCodeId,
            CancellationToken cancellationToken = default)
        {
            var existing = await _enrollmentRepo.GetByStudentAndCourseAsync(studentId, courseId);
            if (existing != null)
                return new AccessCodeEnrollmentResult(false, "already_enrolled", "You already have an enrollment for this course.", existing.Id);

            var course = await _courseRepo.GetByIdAsync(courseId);
            if (course == null || !string.Equals(course.Status, "published", StringComparison.OrdinalIgnoreCase))
                return new AccessCodeEnrollmentResult(false, "course_unavailable", "This course is not available for enrollment.");

            var enrollment = new Enrollment
            {
                StudentId = studentId,
                CourseId = courseId,
                CourseAccessCodeId = courseAccessCodeId,
                AccessType = CourseAccessTypes.Sequential,
                EnrolledAt = DateTime.UtcNow,
                Status = "active",
                ProgressPercent = 0,
                WatchedLessons = new List<WatchedLesson>()
            };

            await _enrollmentRepo.CreateAsync(enrollment);
            await _courseRepo.UpdateStatsAsync(courseId, 1, 0);
            return new AccessCodeEnrollmentResult(true, null, "Access code redeemed. Sequential course access is active.", enrollment.Id);
        }
        private async Task<EnrollmentResult> CreatePaidCheckoutAsync(
            string studentId,
            Course course,
            decimal price,
            string accessType,
            PaymentCustomer customer,
            CancellationToken cancellationToken)
        {
            if (_paymentService.RequiresBillingPhone && !IsValidBillingPhone(customer.PhoneNumber))
            {
                return new EnrollmentResult
                {
                    Success = false,
                    ErrorCode = "payment_phone_required",
                    Message = "A valid billing phone number is required before starting this payment."
                };
            }

            long amountCents;
            string currency;
            try
            {
                amountCents = ToCents(price);
                currency = _paymentService.Currency;
            }
            catch (PaymentCheckoutException exception)
            {
                return PaymentFailure(exception);
            }
            catch (OverflowException)
            {
                return new EnrollmentResult
                {
                    Success = false,
                    ErrorCode = "payment_amount_invalid",
                    Message = "The course price cannot be processed for payment."
                };
            }

            // A browser may resend an enrollment request because of a double
            // click, latency, or a refresh. Reuse the one active hosted
            // checkout instead of creating another chargeable payment session.
            var pendingPayment = await _paymentTransactionRepo.GetPendingByStudentAndCourseAsync(
                _paymentService.Provider,
                studentId,
                course.Id,
                cancellationToken);

            if (pendingPayment != null)
            {
                if (pendingPayment.CreatedAt.AddMinutes(30) > DateTime.UtcNow)
                {
                    if (!string.IsNullOrWhiteSpace(pendingPayment.CheckoutUrl))
                    {
                        return new EnrollmentResult
                        {
                            Success = true,
                            RequiresPayment = true,
                            Provider = pendingPayment.Provider,
                            PaymentReference = pendingPayment.Id,
                            CheckoutUrl = pendingPayment.CheckoutUrl,
                            IsMock = pendingPayment.IsMock,
                            AccessType = pendingPayment.AccessType,
                            Message = "An existing payment checkout is still awaiting completion."
                        };
                    }

                    return new EnrollmentResult
                    {
                        Success = false,
                        ErrorCode = "payment_already_pending",
                        Message = "A payment checkout is already pending. Complete or wait for it to expire before trying again."
                    };
                }

                await _paymentTransactionRepo.MarkFailedAsync(
                    pendingPayment.Id,
                    "payment_checkout_expired",
                    cancellationToken);
            }

            var payment = new PaymentTransaction
            {
                Provider = _paymentService.Provider,
                IsMock = _paymentService.IsMock,
                StudentId = studentId,
                CourseId = course.Id,
                AccessType = accessType,
                Amount = price,
                AmountCents = amountCents,
                Currency = currency,
                Status = "pending",
                ActiveCheckoutKey = $"{_paymentService.Provider}:{studentId}:{course.Id}"
            };

            var activePayment = await _paymentTransactionRepo.GetOrCreatePendingAsync(payment, cancellationToken);
            if (!string.Equals(activePayment.Id, payment.Id, StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(activePayment.CheckoutUrl))
                {
                    return new EnrollmentResult
                    {
                        Success = true,
                        RequiresPayment = true,
                        Provider = activePayment.Provider,
                        PaymentReference = activePayment.Id,
                        CheckoutUrl = activePayment.CheckoutUrl,
                        IsMock = activePayment.IsMock,
                        AccessType = activePayment.AccessType,
                        Message = "An existing payment checkout is still awaiting completion."
                    };
                }

                return new EnrollmentResult
                {
                    Success = false,
                    ErrorCode = "payment_already_pending",
                    Message = "A payment checkout is already pending. Complete or wait for it to expire before trying again."
                };
            }

            try
            {
                var checkout = await _paymentService.CreateCheckoutAsync(
                    new PaymentCheckoutRequest
                    {
                        PaymentReference = payment.Id,
                        StudentId = studentId,
                        CourseId = course.Id,
                        AccessType = accessType,
                        CourseSlug = course.Slug,
                        CourseTitle = course.Title,
                        Amount = price,
                        AmountCents = amountCents,
                        Currency = currency,
                        Customer = customer
                    },
                    cancellationToken);

                await _paymentTransactionRepo.UpdateCheckoutAsync(
                    payment.Id,
                    // Kashier's hosted session URL lets the authenticated student
                    // resume the same checkout rather than create another charge.
                    checkout.CheckoutUrl,
                    checkout.ProviderOrderId,
                    checkout.ProviderIntentionId,
                    checkout.ProviderIntegrationId,
                    cancellationToken);

                return new EnrollmentResult
                {
                    Success = true,
                    RequiresPayment = true,
                    Provider = checkout.Provider,
                    PaymentReference = payment.Id,
                    CheckoutUrl = checkout.CheckoutUrl,
                    IsMock = checkout.IsMock,
                    AccessType = accessType,
                    Message = checkout.IsMock
                        ? "Development payment confirmation is required. No real charge will be made."
                        : $"Payment required. Complete checkout with {checkout.Provider}."
                };
            }
            catch (PaymentCheckoutException exception)
            {
                await _paymentTransactionRepo.MarkFailedAsync(payment.Id, exception.Code, cancellationToken);
                return PaymentFailure(exception);
            }
            catch (Exception exception)
            {
                await _paymentTransactionRepo.MarkFailedAsync(payment.Id, "payment_provider_error", cancellationToken);
                _logger.LogError(exception, "Payment checkout creation failed for course {CourseId}", course.Id);
                return new EnrollmentResult
                {
                    Success = false,
                    ErrorCode = "payment_provider_error",
                    Message = "The payment provider could not create a checkout. Please try again later."
                };
            }
        }

        public async Task<PaymentCompletionResult> ConfirmMockPaymentAsync(
            string studentId,
            string paymentReference,
            CancellationToken cancellationToken = default)
        {
            var payment = await _paymentTransactionRepo.GetByIdAsync(paymentReference, cancellationToken);
            if (payment == null || !payment.IsMock ||
                !string.Equals(payment.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentCompletionFailure("payment_not_found", "The development payment could not be found.");
            }

            if (!string.Equals(payment.StudentId, studentId, StringComparison.Ordinal))
            {
                return PaymentCompletionFailure("payment_not_owned", "This payment does not belong to the current student.");
            }

            if (!_paymentService.IsMock)
            {
                return PaymentCompletionFailure("mock_payment_disabled", "Development payment confirmation is not available with the active provider.");
            }

            if (payment.EnrollmentId is not null)
            {
                return PaymentCompletionSuccess(payment.EnrollmentId, "Mock", true, "Development payment was already completed.", payment.AccessType);
            }

            var confirmation = await _paymentService.ConfirmDevelopmentPaymentAsync(
                CreateCheckoutRequest(payment),
                cancellationToken);
            return await FinalizeConfirmedPaymentAsync(payment, confirmation, cancellationToken);
        }

        public async Task<PaymentCompletionResult?> ConfirmPaymobPaymentAsync(
            PaymobTransactionCallback callback,
            CancellationToken cancellationToken = default)
        {
            var payment = await _paymentTransactionRepo.GetByProviderOrderIdAsync(
                "Paymob",
                callback.ProviderOrderId,
                cancellationToken);

            // Returning null lets the webhook acknowledge an unrelated but valid
            // Paymob callback without granting any local entitlement.
            if (payment == null) return null;

            if (!string.Equals(payment.ProviderIntegrationId, callback.IntegrationId, StringComparison.Ordinal) ||
                payment.AmountCents != callback.AmountCents ||
                !string.Equals(payment.Currency, callback.Currency, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Rejected Paymob callback {TransactionId} because it did not match payment {PaymentId}",
                    callback.ProviderTransactionId,
                    payment.Id);
                return PaymentCompletionFailure("payment_callback_mismatch", "The payment callback did not match the expected order.");
            }

            var confirmation = new PaymentProviderConfirmation
            {
                Provider = "Paymob",
                ProviderTransactionId = callback.ProviderTransactionId,
                ProviderOrderId = callback.ProviderOrderId,
                AmountCents = callback.AmountCents,
                Currency = callback.Currency,
                IsSuccessful = callback.IsConfirmedSuccess,
                IsFinalFailure = callback.IsFinalFailure
            };

            return await FinalizeConfirmedPaymentAsync(payment, confirmation, cancellationToken);
        }

        public async Task<PaymentCompletionResult?> ConfirmKashierPaymentAsync(
            KashierTransactionCallback callback,
            CancellationToken cancellationToken = default)
        {
            var payment = await _paymentTransactionRepo.GetByProviderOrderIdAsync(
                "Kashier",
                callback.ProviderOrderId,
                cancellationToken);

            // A valid Kashier event for an order not created by this application
            // is acknowledged but can never provision local course access.
            if (payment == null) return null;

            if (payment.AmountCents != callback.AmountCents ||
                !string.Equals(payment.Currency, callback.Currency, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Rejected Kashier callback {TransactionId} because it did not match payment {PaymentId}",
                    callback.ProviderTransactionId,
                    payment.Id);
                return PaymentCompletionFailure("payment_callback_mismatch", "The payment callback did not match the expected order.");
            }

            var confirmation = new PaymentProviderConfirmation
            {
                Provider = "Kashier",
                ProviderTransactionId = callback.ProviderTransactionId,
                ProviderOrderId = callback.ProviderOrderId,
                AmountCents = callback.AmountCents,
                Currency = callback.Currency,
                IsSuccessful = callback.IsConfirmedSuccess,
                IsFinalFailure = callback.IsFinalFailure
            };

            return await FinalizeConfirmedPaymentAsync(payment, confirmation, cancellationToken);
        }
        private async Task<PaymentCompletionResult> FinalizeConfirmedPaymentAsync(
            PaymentTransaction payment,
            PaymentProviderConfirmation confirmation,
            CancellationToken cancellationToken)
        {
            if (!string.Equals(payment.Provider, confirmation.Provider, StringComparison.OrdinalIgnoreCase) ||
                payment.AmountCents != confirmation.AmountCents ||
                !string.Equals(payment.Currency, confirmation.Currency, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(payment.ProviderOrderId) &&
                 !string.Equals(payment.ProviderOrderId, confirmation.ProviderOrderId, StringComparison.Ordinal)))
            {
                return PaymentCompletionFailure("payment_callback_mismatch", "The payment confirmation did not match the expected order.");
            }

            if (!confirmation.IsSuccessful)
            {
                if (confirmation.IsFinalFailure)
                {
                    await _paymentTransactionRepo.MarkFailedAsync(payment.Id, "payment_not_successful", cancellationToken);
                }

                return PaymentCompletionFailure("payment_not_successful", "Payment was not confirmed. Enrollment was not created.");
            }

            var succeeded = await _paymentTransactionRepo.MarkSucceededAsync(
                payment.Id,
                confirmation.ProviderTransactionId,
                cancellationToken);
            if (succeeded == null)
            {
                return PaymentCompletionFailure("payment_state_invalid", "The payment could not be finalized.");
            }

            if (!string.IsNullOrWhiteSpace(succeeded.EnrollmentId))
            {
                return PaymentCompletionSuccess(
                    succeeded.EnrollmentId,
                    succeeded.Provider,
                    succeeded.IsMock,
                    "Payment was already confirmed and enrollment is active.",
                    succeeded.AccessType);
            }

            var claimToken = Guid.NewGuid().ToString("N");
            if (!await _paymentTransactionRepo.TryClaimFulfillmentAsync(succeeded.Id, claimToken, cancellationToken))
            {
                var current = await _paymentTransactionRepo.GetByIdAsync(succeeded.Id, cancellationToken);
                if (!string.IsNullOrWhiteSpace(current?.EnrollmentId))
                {
                    return PaymentCompletionSuccess(
                        current.EnrollmentId,
                        current.Provider,
                        current.IsMock,
                        "Payment was already confirmed and enrollment is active.",
                        current.AccessType);
                }

                // A parallel callback is doing the same work. It is never treated
                // as successful by the browser; a later read will show enrollment.
                return PaymentCompletionFailure("payment_finalizing", "Payment is being finalized.");
            }

            try
            {
                var existingEnrollment = await _enrollmentRepo.GetByStudentAndCourseAsync(
                    succeeded.StudentId,
                    succeeded.CourseId);
                if (existingEnrollment != null)
                {
                    // A cancelled enrollment must be reactivated so the student
                    // actually receives course access after paying again.
                    if (string.Equals(existingEnrollment.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                    {
                        existingEnrollment.Status = "active";
                        existingEnrollment.AccessType = CourseAccessTypes.Resolve(succeeded.AccessType);
                        existingEnrollment.EnrolledAt = DateTime.UtcNow;
                        existingEnrollment.CompletedAt = null;
                        await _enrollmentRepo.UpdateAsync(existingEnrollment.Id, existingEnrollment);
                        await _courseRepo.UpdateStatsAsync(succeeded.CourseId, 1, 0);
                        _logger.LogInformation(
                            "Reactivated cancelled enrollment {EnrollmentId} from confirmed {Provider} payment {PaymentId}",
                            existingEnrollment.Id,
                            succeeded.Provider,
                            succeeded.Id);
                    }

                    await _paymentTransactionRepo.SetEnrollmentAsync(
                        succeeded.Id,
                        claimToken,
                        existingEnrollment.Id,
                        cancellationToken);
                    return PaymentCompletionSuccess(
                        existingEnrollment.Id,
                        succeeded.Provider,
                        succeeded.IsMock,
                        "Payment was confirmed and enrollment is active.",
                        existingEnrollment.AccessType);
                }

                var course = await _courseRepo.GetByIdAsync(succeeded.CourseId);
                if (course == null || course.Status != "published")
                {
                    await _paymentTransactionRepo.ReleaseFulfillmentClaimAsync(succeeded.Id, claimToken, cancellationToken);
                    _logger.LogError(
                        "Confirmed payment {PaymentId} could not be fulfilled because course {CourseId} is unavailable",
                        succeeded.Id,
                        succeeded.CourseId);
                    return PaymentCompletionFailure(
                        "payment_manual_review_required",
                        "Payment was confirmed but course access requires support review.");
                }

                var enrollment = new Enrollment
                {
                    StudentId = succeeded.StudentId,
                    CourseId = succeeded.CourseId,
                    AccessType = CourseAccessTypes.Resolve(succeeded.AccessType),
                    EnrolledAt = DateTime.UtcNow,
                    Status = "active",
                    ProgressPercent = 0,
                    WatchedLessons = new List<WatchedLesson>()
                };

                await _enrollmentRepo.CreateAsync(enrollment);
                await _courseRepo.UpdateStatsAsync(succeeded.CourseId, 1, 0);
                await _paymentTransactionRepo.SetEnrollmentAsync(
                    succeeded.Id,
                    claimToken,
                    enrollment.Id,
                    cancellationToken);

                _logger.LogInformation(
                    "Created enrollment {EnrollmentId} from confirmed {Provider} payment {PaymentId}",
                    enrollment.Id,
                    succeeded.Provider,
                    succeeded.Id);
                return PaymentCompletionSuccess(
                    enrollment.Id,
                    succeeded.Provider,
                    succeeded.IsMock,
                    succeeded.IsMock
                        ? "Development payment completed. Enrollment is active."
                        : "Payment confirmed. Enrollment is active.",
                    enrollment.AccessType);
            }
            catch (Exception exception)
            {
                await _paymentTransactionRepo.ReleaseFulfillmentClaimAsync(succeeded.Id, claimToken, cancellationToken);
                _logger.LogError(exception, "Failed to fulfill confirmed payment {PaymentId}", succeeded.Id);
                throw;
            }
        }

        private static PaymentCheckoutRequest CreateCheckoutRequest(PaymentTransaction payment) => new()
        {
            PaymentReference = payment.Id,
            StudentId = payment.StudentId,
            CourseId = payment.CourseId,
            AccessType = CourseAccessTypes.Resolve(payment.AccessType),
            Amount = payment.Amount,
            AmountCents = payment.AmountCents,
            Currency = payment.Currency
        };

        private static EnrollmentResult PaymentFailure(PaymentCheckoutException exception) => new()
        {
            Success = false,
            ErrorCode = exception.Code,
            Message = exception.Message
        };

        private static PaymentCompletionResult PaymentCompletionFailure(string code, string message) => new()
        {
            Success = false,
            Code = code,
            Message = message
        };

        private static PaymentCompletionResult PaymentCompletionSuccess(
            string enrollmentId,
            string provider,
            bool isMock,
            string message,
            string? accessType = null) => new()
        {
            Success = true,
            EnrollmentId = enrollmentId,
            Provider = provider,
            IsMock = isMock,
            AccessType = CourseAccessTypes.Resolve(accessType),
            Message = message
        };

        private static bool IsValidBillingPhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Trim();
            return normalized.Length is >= 7 and <= 32 &&
                   normalized.All(character => char.IsDigit(character) || character is '+' or '-' or ' ' or '(' or ')');
        }

        private static long ToCents(decimal amount)
        {
            if (amount <= 0) throw new OverflowException();
            var cents = decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
            return checked((long)cents);
        }

        public async Task<PaymentStatusResult> GetPaymentStatusAsync(
            string studentId,
            string paymentReference,
            CancellationToken cancellationToken = default)
        {
            var payment = await _paymentTransactionRepo.GetByIdAsync(paymentReference, cancellationToken);
            if (payment == null || !string.Equals(payment.StudentId, studentId, StringComparison.Ordinal))
            {
                return new PaymentStatusResult
                {
                    PaymentReference = paymentReference,
                    Status = "not_found",
                    Message = "Payment not found."
                };
            }

            return new PaymentStatusResult
            {
                PaymentReference = payment.Id,
                Status = payment.Status,
                EnrollmentId = payment.EnrollmentId,
                AccessType = payment.AccessType is not null
                    ? CourseAccessTypes.Resolve(payment.AccessType)
                    : null,
                Message = payment.Status switch
                {
                    "succeeded" when !string.IsNullOrWhiteSpace(payment.EnrollmentId)
                        => "Payment confirmed. Enrollment is active.",
                    "succeeded" => "Payment confirmed. Enrollment is being finalized.",
                    "pending" => "Payment is still being processed.",
                    "failed" => "Payment was not successful.",
                    _ => "Payment status is unknown."
                }
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

            var dto = _mapper.Map<EnrollmentDto>(enrollment);
            dto.AccessType = CourseAccessTypes.Resolve(enrollment.AccessType);
            return dto;
        }

        public async Task<LessonProgressUpdateResult> UpdateLessonProgressAsync(
            string studentId,
            string courseId,
            string lessonId,
            int watchDurationSeconds)
        {
            if (watchDurationSeconds < 0)
                throw new BadRequestException("Watch duration cannot be negative.");

            var enrollment = await _enrollmentRepo.GetActiveByStudentAndCourseAsync(studentId, courseId);
            if (enrollment == null)
                throw new UnauthorizedException("An active enrollment is required.");

            var lesson = await _lessonRepo.GetByIdAsync(lessonId);
            if (lesson == null)
                throw new NotFoundException("Lesson not found.");
            if (!string.Equals(lesson.CourseId, courseId, StringComparison.Ordinal))
                throw new BadRequestException("Lesson does not belong to this course.");

            var access = await _courseLessonAccessService.CanAccessAsync(studentId, courseId, lessonId);
            if (!access.IsAllowed)
            {
                if (access.IsLocked)
                    throw new ForbiddenException(access.DenialMessage ?? "This lesson is locked.");
                throw new UnauthorizedException(access.DenialMessage ?? "Lesson access is not available.");
            }

            var now = DateTime.UtcNow;
            var existing = enrollment.WatchedLessons.FirstOrDefault(item => item.LessonId == lessonId);
            var storedSeconds = Math.Max(0, existing?.WatchDurationSeconds ?? 0);
            int recordedSeconds;
            bool isCompleted;

            if (lesson.VideoDurationSeconds > 0)
            {
                var duration = lesson.VideoDurationSeconds;
                var requestedSeconds = Math.Clamp(watchDurationSeconds, 0, duration);
                var elapsedSeconds = existing is null
                    ? 0d
                    : Math.Max(0d, (now - existing.WatchedAt).TotalSeconds);
                var allowedIncrease = existing is null
                    ? _lessonProgressOptions.InitialProgressAllowanceSeconds
                    : (int)Math.Ceiling(elapsedSeconds * (double)_lessonProgressOptions.MaximumPlaybackRate);
                var maximumRecorded = Math.Min(duration, storedSeconds + Math.Max(0, allowedIncrease));
                recordedSeconds = Math.Min(Math.Max(storedSeconds, requestedSeconds), maximumRecorded);
                var completionThreshold = (int)Math.Ceiling(duration * (_lessonProgressOptions.VideoCompletionPercent / 100m));
                isCompleted = existing?.IsCompleted == true || recordedSeconds >= completionThreshold;
            }
            else if (!string.IsNullOrWhiteSpace(lesson.VideoUrl))
            {
                // A video with no configured server-side duration cannot prove
                // completion. It remains playable, but cannot unlock the next
                // sequential lesson until an admin supplies its duration.
                throw new BadRequestException("This lesson video needs a configured duration before it can be completed.");
            }
            else
            {
                // Text-only lessons have no measurable video timeline. The
                // existing explicit completion action remains available after
                // server-side entitlement has been established.
                recordedSeconds = 0;
                isCompleted = true;
            }

            var watchedLesson = new WatchedLesson
            {
                LessonId = lesson.Id,
                LessonTitle = lesson.Title,
                WatchedAt = now,
                WatchDurationSeconds = recordedSeconds,
                IsCompleted = isCompleted
            };

            var lessons = await _courseLessonAccessService.GetOrderedLessonsAsync(courseId);
            var updatedWatchedLessons = enrollment.WatchedLessons
                .Where(item => item.LessonId != lessonId)
                .ToList();
            updatedWatchedLessons.Add(watchedLesson);

            var completedLessons = updatedWatchedLessons.Count(item =>
                item.IsCompleted && lessons.Any(lessonItem => lessonItem.Id == item.LessonId));
            enrollment.WatchedLessons = updatedWatchedLessons;
            enrollment.ProgressPercent = lessons.Count == 0
                ? 0
                : Math.Min(100, (decimal)completedLessons / lessons.Count * 100);
            enrollment.LastWatchedLessonId = lessonId;
            enrollment.LastWatchedAt = now;

            if (lessons.Count > 0 && completedLessons == lessons.Count)
            {
                enrollment.ProgressPercent = 100;
                enrollment.Status = "completed";
                enrollment.CompletedAt ??= now;
            }
            else
            {
                enrollment.Status = "active";
                enrollment.CompletedAt = null;
            }

            await _enrollmentRepo.UpdateAsync(enrollment.Id, enrollment);

            var lessonIndex = lessons.FindIndex(item => item.Id == lessonId);
            var nextLessonId = CourseAccessTypes.Resolve(enrollment.AccessType) == CourseAccessTypes.Sequential &&
                               isCompleted && lessonIndex >= 0 && lessonIndex + 1 < lessons.Count
                ? lessons[lessonIndex + 1].Id
                : null;

            return new LessonProgressUpdateResult
            {
                WatchDurationSeconds = recordedSeconds,
                IsCompleted = isCompleted,
                ProgressPercent = enrollment.ProgressPercent,
                AccessType = CourseAccessTypes.Resolve(enrollment.AccessType),
                NextLessonId = nextLessonId
            };
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
                    AccessType = CourseAccessTypes.Resolve(enrollment.AccessType),

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
