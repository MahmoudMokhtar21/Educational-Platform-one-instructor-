namespace Educatinal_Platform.DTOs
{
    public class EnrollmentResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public string? EnrollmentId { get; set; }
            public string? AccessType { get; set; }
            public bool RequiresPayment { get; set; }
            public string? Provider { get; set; }
            public string? PaymentReference { get; set; }
            public string? CheckoutUrl { get; set; }
            public bool IsMock { get; set; }
            public string? ErrorCode { get; set; }
    }
 }


