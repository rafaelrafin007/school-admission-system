using SchoolAdmission.Application.Holds.DTOs;

namespace SchoolAdmission.Application.Holds;

public enum CreateHoldOutcome
{
    Created,
    ClassNotFound,
    DuplicateApplication,
    ClassFull
}

public sealed record CreateHoldResult(CreateHoldOutcome Outcome, CreateHoldResponse? Hold = null);
