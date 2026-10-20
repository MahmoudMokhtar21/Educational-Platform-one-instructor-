using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System.Text.Json.Serialization;

namespace Educatinal_Platform.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConsultationPriority
{
    Unspecified = 0,
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConsultationType
{
    Unspecified = 0,
    ExecutiveConsulting = 1,
    FieldConsulting = 2,
    RemoteConsulting = 3,
    ConsultingContract = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InterviewType
{
    Unspecified = 0,
    Phone = 1,
    LiveZoom = 2,
    FieldVisit = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConsultationRequestStatus
{
    Unspecified = 0,
    Pending = 1,
    Contacted = 2,
    Confirmed = 3,
    Completed = 4,
    Declined = 5,
    Cancelled = 6
}

/// <summary>
/// An anonymous visitor's request for a business consultation. It deliberately
/// contains no payment, authentication, credential, or account data.
/// </summary>
public class ConsultationRequest
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string EmailAddress { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string WhatsappNumber { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public ConsultationPriority Priority { get; set; }

    public string BusinessActivity { get; set; } = string.Empty;
    public string MainProductsOrServices { get; set; } = string.Empty;
    public string ConsultationTopic { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public ConsultationType ConsultationType { get; set; }

    public string BusinessName { get; set; } = string.Empty;
    public string BusinessField { get; set; } = string.Empty;

    [BsonDateOnlyOptions(BsonType.String, DateOnlyDocumentFormat.YearMonthDay)]
    public DateOnly EstablishmentDate { get; set; }

    public int? NumberOfPartners { get; set; }
    public string PositionInCompany { get; set; } = string.Empty;
    public string ReasonForConsultation { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public InterviewType InterviewType { get; set; }

    [BsonDateOnlyOptions(BsonType.String, DateOnlyDocumentFormat.YearMonthDay)]
    public DateOnly PreferredInterviewDate { get; set; }

    [BsonTimeOnlyOptions(BsonType.String)]
    public TimeOnly PreferredTimeFrom { get; set; }

    [BsonTimeOnlyOptions(BsonType.String)]
    public TimeOnly PreferredTimeTo { get; set; }

    [BsonRepresentation(BsonType.String)]
    public ConsultationRequestStatus Status { get; set; } = ConsultationRequestStatus.Pending;

    public string? AdminNotes { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
