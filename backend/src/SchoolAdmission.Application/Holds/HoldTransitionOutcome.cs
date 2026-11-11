namespace SchoolAdmission.Application.Holds;

public enum HoldTransitionOutcome
{
    Succeeded,
    NotFound,
    Forbidden,
    InvalidState
}
