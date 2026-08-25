namespace Educatinal_Platform.DTOs
{
    public class EnrollmentResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public string? EnrollmentId { get; set; }
            public bool RequiresPayment { get; set; }
            public string? PaymentIntentId { get; set; }
            public string? ClientSecret { get; set; }
    }
 }


