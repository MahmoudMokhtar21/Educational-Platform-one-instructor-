using Educatinal_Platform.DTOs;
using Educatinal_Platform.Exceptions;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Educatinal_Platform.Services;

public interface IConsultationRequestService
{
    Task<ConsultationRequestSubmissionDto> CreateAsync(CreateConsultationRequestDto dto);
    Task<List<ConsultationRequestListItemDto>> GetAllAsync(ConsultationRequestStatus? status = null);
    Task<ConsultationRequestDetailDto> GetByIdAsync(string id);
    Task<ConsultationRequestDetailDto> UpdateStatusAsync(string id, UpdateConsultationRequestStatusDto dto);
}

public class ConsultationRequestService(IConsultationRequestRepository repository) : IConsultationRequestService
{
    private static readonly EmailAddressAttribute EmailValidator = new();
    private static readonly Regex WhatsappPattern = new(
        @"^\+?[0-9][0-9\s().-]{6,31}$",
        RegexOptions.CultureInvariant);

    public async Task<ConsultationRequestSubmissionDto> CreateAsync(CreateConsultationRequestDto dto)
    {
        if (dto is null)
        {
            throw new BadRequestException("A consultation request is required.");
        }

        ValidateCreate(dto);

        var request = new ConsultationRequest
        {
            EmailAddress = NormalizeEmail(dto.EmailAddress),
            CompanyName = RequiredText(dto.CompanyName, "Company name", 200),
            WhatsappNumber = NormalizeWhatsappNumber(dto.WhatsappNumber),
            Priority = dto.Priority!.Value,
            BusinessActivity = RequiredText(dto.BusinessActivity, "Business activity", 500),
            MainProductsOrServices = RequiredText(dto.MainProductsOrServices, "Main products or services", 2000),
            ConsultationTopic = RequiredText(dto.ConsultationTopic, "Consultation topic", 300),
            ConsultationType = dto.ConsultationType!.Value,
            BusinessName = RequiredText(dto.BusinessName, "Business name", 200),
            BusinessField = RequiredText(dto.BusinessField, "Business field", 200),
            EstablishmentDate = dto.EstablishmentDate!.Value,
            NumberOfPartners = dto.NumberOfPartners,
            PositionInCompany = RequiredText(dto.PositionInCompany, "Position in company", 200),
            ReasonForConsultation = RequiredText(dto.ReasonForConsultation, "Reason for consultation", 4000),
            InterviewType = dto.InterviewType!.Value,
            PreferredInterviewDate = dto.PreferredInterviewDate!.Value,
            PreferredTimeFrom = dto.PreferredTimeFrom!.Value,
            PreferredTimeTo = dto.PreferredTimeTo!.Value,
            Status = ConsultationRequestStatus.Pending
        };

        await repository.CreateAsync(request);

        return new ConsultationRequestSubmissionDto
        {
            Id = request.Id,
            Status = request.Status,
            SubmittedAt = request.SubmittedAt
        };
    }

    public async Task<List<ConsultationRequestListItemDto>> GetAllAsync(ConsultationRequestStatus? status = null)
    {
        ValidateStatusFilter(status);
        return (await repository.GetAllAsync(status)).Select(MapList).ToList();
    }

    public async Task<ConsultationRequestDetailDto> GetByIdAsync(string id) =>
        MapDetail(await FindRequiredAsync(id));

    public async Task<ConsultationRequestDetailDto> UpdateStatusAsync(string id, UpdateConsultationRequestStatusDto dto)
    {
        if (dto is null || dto.Status is null || !IsAllowedStatus(dto.Status.Value))
        {
            throw new BadRequestException("A valid consultation request status is required.");
        }

        if (dto.AdminNotes?.Trim().Length > 2000)
        {
            throw new BadRequestException("Admin notes must be 2000 characters or fewer.");
        }

        var request = await FindRequiredAsync(id);
        request.Status = dto.Status.Value;
        if (dto.AdminNotes is not null)
        {
            request.AdminNotes = NullIfBlank(dto.AdminNotes);
        }

        request.UpdatedAt = DateTime.UtcNow;
        await repository.ReplaceAsync(request);

        return MapDetail(request);
    }

    private async Task<ConsultationRequest> FindRequiredAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new BadRequestException("Consultation request id is required.");
        }

        return await repository.GetByIdAsync(id.Trim())
            ?? throw new NotFoundException("Consultation request not found.");
    }

    private static void ValidateCreate(CreateConsultationRequestDto dto)
    {
        _ = NormalizeEmail(dto.EmailAddress);
        _ = RequiredText(dto.CompanyName, "Company name", 200);
        _ = NormalizeWhatsappNumber(dto.WhatsappNumber);
        ValidateEnum(dto.Priority, "priority", IsAllowedPriority);
        _ = RequiredText(dto.BusinessActivity, "Business activity", 500);
        _ = RequiredText(dto.MainProductsOrServices, "Main products or services", 2000);
        _ = RequiredText(dto.ConsultationTopic, "Consultation topic", 300);
        ValidateEnum(dto.ConsultationType, "consultation type", IsAllowedConsultationType);
        _ = RequiredText(dto.BusinessName, "Business name", 200);
        _ = RequiredText(dto.BusinessField, "Business field", 200);

        if (dto.EstablishmentDate is null)
        {
            throw new BadRequestException("An establishment date is required.");
        }

        if (dto.EstablishmentDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new BadRequestException("The establishment date cannot be in the future.");
        }

        if (dto.NumberOfPartners is < 0)
        {
            throw new BadRequestException("Number of partners cannot be negative.");
        }

        _ = RequiredText(dto.PositionInCompany, "Position in company", 200);
        _ = RequiredText(dto.ReasonForConsultation, "Reason for consultation", 4000);
        ValidateEnum(dto.InterviewType, "interview type", IsAllowedInterviewType);

        if (dto.PreferredInterviewDate is null)
        {
            throw new BadRequestException("A preferred interview date is required.");
        }

        if (dto.PreferredInterviewDate.Value < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new BadRequestException("The preferred interview date cannot be in the past.");
        }

        if (dto.PreferredTimeFrom is null || dto.PreferredTimeTo is null)
        {
            throw new BadRequestException("Both preferred interview times are required.");
        }

        if (dto.PreferredTimeTo.Value <= dto.PreferredTimeFrom.Value)
        {
            throw new BadRequestException("Preferred interview end time must be later than the start time.");
        }
    }

    private static void ValidateStatusFilter(ConsultationRequestStatus? status)
    {
        if (status is not null && !IsAllowedStatus(status.Value))
        {
            throw new BadRequestException("The consultation request status filter is invalid.");
        }
    }

    private static void ValidateEnum<TEnum>(TEnum? value, string label, Func<TEnum, bool> isAllowed)
        where TEnum : struct, Enum
    {
        if (value is null || !isAllowed(value.Value))
        {
            throw new BadRequestException($"A valid {label} is required.");
        }
    }

    private static bool IsAllowedPriority(ConsultationPriority value) =>
        value != ConsultationPriority.Unspecified && Enum.IsDefined(value);

    private static bool IsAllowedConsultationType(ConsultationType value) =>
        value != ConsultationType.Unspecified && Enum.IsDefined(value);

    private static bool IsAllowedInterviewType(InterviewType value) =>
        value != InterviewType.Unspecified && Enum.IsDefined(value);

    private static bool IsAllowedStatus(ConsultationRequestStatus value) =>
        value != ConsultationRequestStatus.Unspecified && Enum.IsDefined(value);

    private static string RequiredText(string? value, string label, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maxLength)
        {
            throw new BadRequestException($"{label} is required and must be {maxLength} characters or fewer.");
        }

        return normalized;
    }

    private static string NormalizeEmail(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > 254 ||
            normalized.Any(char.IsWhiteSpace) || !EmailValidator.IsValid(normalized))
        {
            throw new BadRequestException("A valid email address is required.");
        }

        return normalized;
    }

    private static string NormalizeWhatsappNumber(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (!WhatsappPattern.IsMatch(normalized))
        {
            throw new BadRequestException("A valid WhatsApp number is required.");
        }

        return normalized;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ConsultationRequestListItemDto MapList(ConsultationRequest request) => new()
    {
        Id = request.Id,
        CompanyName = request.CompanyName,
        EmailAddress = request.EmailAddress,
        Priority = request.Priority,
        ConsultationType = request.ConsultationType,
        InterviewType = request.InterviewType,
        PreferredInterviewDate = request.PreferredInterviewDate,
        PreferredTimeFrom = request.PreferredTimeFrom,
        PreferredTimeTo = request.PreferredTimeTo,
        Status = request.Status,
        SubmittedAt = request.SubmittedAt,
        UpdatedAt = request.UpdatedAt
    };

    private static ConsultationRequestDetailDto MapDetail(ConsultationRequest request) => new()
    {
        Id = request.Id,
        EmailAddress = request.EmailAddress,
        CompanyName = request.CompanyName,
        WhatsappNumber = request.WhatsappNumber,
        Priority = request.Priority,
        BusinessActivity = request.BusinessActivity,
        MainProductsOrServices = request.MainProductsOrServices,
        ConsultationTopic = request.ConsultationTopic,
        ConsultationType = request.ConsultationType,
        BusinessName = request.BusinessName,
        BusinessField = request.BusinessField,
        EstablishmentDate = request.EstablishmentDate,
        NumberOfPartners = request.NumberOfPartners,
        PositionInCompany = request.PositionInCompany,
        ReasonForConsultation = request.ReasonForConsultation,
        InterviewType = request.InterviewType,
        PreferredInterviewDate = request.PreferredInterviewDate,
        PreferredTimeFrom = request.PreferredTimeFrom,
        PreferredTimeTo = request.PreferredTimeTo,
        Status = request.Status,
        AdminNotes = request.AdminNotes,
        SubmittedAt = request.SubmittedAt,
        UpdatedAt = request.UpdatedAt
    };
}
