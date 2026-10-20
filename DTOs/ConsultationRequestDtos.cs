using Educatinal_Platform.Models;
using Educatinal_Platform.Serialization;
using System.Text.Json.Serialization;

namespace Educatinal_Platform.DTOs;

/// <summary>
/// Public request payload. Only business and scheduling information is
/// accepted; status, notes, identifiers, payment, and auth data are server or
/// admin controlled.
/// </summary>
public sealed class CreateConsultationRequestDto
{
    public string EmailAddress { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string WhatsappNumber { get; set; } = string.Empty;
    public ConsultationPriority? Priority { get; set; }
    public string BusinessActivity { get; set; } = string.Empty;
    public string MainProductsOrServices { get; set; } = string.Empty;
    public string ConsultationTopic { get; set; } = string.Empty;
    public ConsultationType? ConsultationType { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessField { get; set; } = string.Empty;
    public DateOnly? EstablishmentDate { get; set; }
    public int? NumberOfPartners { get; set; }
    public string PositionInCompany { get; set; } = string.Empty;
    public string ReasonForConsultation { get; set; } = string.Empty;
    public InterviewType? InterviewType { get; set; }
    public DateOnly? PreferredInterviewDate { get; set; }
    [JsonConverter(typeof(HourMinuteNullableTimeOnlyJsonConverter))]
    public TimeOnly? PreferredTimeFrom { get; set; }

    [JsonConverter(typeof(HourMinuteNullableTimeOnlyJsonConverter))]
    public TimeOnly? PreferredTimeTo { get; set; }
}

public sealed class UpdateConsultationRequestStatusDto
{
    public ConsultationRequestStatus? Status { get; set; }
    public string? AdminNotes { get; set; }
}

/// <summary>
/// Small acknowledgement for the anonymous sender. The full business request
/// is never returned from the public endpoint.
/// </summary>
public sealed class ConsultationRequestSubmissionDto
{
    public string Id { get; set; } = string.Empty;
    public ConsultationRequestStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class ConsultationRequestListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public ConsultationPriority Priority { get; set; }
    public ConsultationType ConsultationType { get; set; }
    public InterviewType InterviewType { get; set; }
    public DateOnly PreferredInterviewDate { get; set; }
    public TimeOnly PreferredTimeFrom { get; set; }
    public TimeOnly PreferredTimeTo { get; set; }
    public ConsultationRequestStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ConsultationRequestDetailDto : ConsultationRequestListItemDto
{
    public string WhatsappNumber { get; set; } = string.Empty;
    public string BusinessActivity { get; set; } = string.Empty;
    public string MainProductsOrServices { get; set; } = string.Empty;
    public string ConsultationTopic { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessField { get; set; } = string.Empty;
    public DateOnly EstablishmentDate { get; set; }
    public int? NumberOfPartners { get; set; }
    public string PositionInCompany { get; set; } = string.Empty;
    public string ReasonForConsultation { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
}
