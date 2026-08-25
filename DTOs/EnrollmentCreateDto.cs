namespace Educatinal_Platform.DTOs
{
    public class EnrollmentCreateDto
    {
        public string CourseId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;

        //OPTIONAL: If it is getten from Stripe Webhook
        public string? PaymentIntentId { get; set; }
        public string? TransactionId { get; set; }
    }

  
}
