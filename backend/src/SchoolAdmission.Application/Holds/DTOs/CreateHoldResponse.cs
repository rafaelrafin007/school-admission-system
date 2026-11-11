using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.Application.Holds.DTOs;

public sealed record CreateHoldResponse(string HoldId, string ClassId, string StudentId, HoldStatus Status, DateTime ExpiresAt);
