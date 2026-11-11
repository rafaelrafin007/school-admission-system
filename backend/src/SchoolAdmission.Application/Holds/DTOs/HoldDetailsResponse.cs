namespace SchoolAdmission.Application.Holds.DTOs;

public sealed record HoldDetailsResponse(string HoldId, string ClassId, string Status, DateTime ExpiresAt);
