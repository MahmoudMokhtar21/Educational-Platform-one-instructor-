namespace Educatinal_Platform.DTOs;
public sealed record AccessCodeEnrollmentResult(bool Success, string? ErrorCode, string Message, string? EnrollmentId = null);