namespace Educatinal_Platform.DTOs
{
    public class EnrollmentCreateDto
    {
        public string CourseId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;

        // Optional provider transaction metadata for internal/admin workflows.
        public string? PaymentReference { get; set; }
        public string? TransactionId { get; set; }
    }

  
}
