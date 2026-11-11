using System.ComponentModel.DataAnnotations;
using SchoolAdmission.Application.Holds.DTOs;
using SchoolAdmission.Application.Holds.Interfaces;

namespace SchoolAdmission.Application.Holds;

public sealed class HoldService(IHoldRepository repository)
{
    public Task<CreateHoldResult> CreateAsync(string classId, string? studentId, CancellationToken cancellationToken)
    {
        var normalizedStudentId = ValidateStudentId(studentId);

        if (string.IsNullOrWhiteSpace(classId) || classId.Length is <= 2 or > 40 ||
            !classId.StartsWith("c_", StringComparison.Ordinal) || classId.Any(char.IsControl))
        {
            return Task.FromResult(new CreateHoldResult(CreateHoldOutcome.ClassNotFound));
        }

        return repository.CreateAsync(classId, normalizedStudentId, cancellationToken);
    }

    public Task<HoldDetailsResponse?> GetByIdAsync(string holdId, CancellationToken cancellationToken) =>
        IsValidHoldId(holdId) ? repository.GetByIdAsync(holdId, cancellationToken) : Task.FromResult<HoldDetailsResponse?>(null);

    public Task<IReadOnlyList<MyHoldResponse>> GetForStudentAsync(string? studentId, CancellationToken cancellationToken) =>
        repository.GetForStudentAsync(ValidateStudentId(studentId), cancellationToken);

    public Task<HoldTransitionOutcome> ConfirmAsync(string holdId, string? studentId, CancellationToken cancellationToken)
    {
        var normalizedStudentId = ValidateStudentId(studentId);
        return IsValidHoldId(holdId) ? repository.ConfirmAsync(holdId, normalizedStudentId, cancellationToken)
            : Task.FromResult(HoldTransitionOutcome.NotFound);
    }

    public Task<HoldTransitionOutcome> CancelAsync(string holdId, string? studentId, CancellationToken cancellationToken)
    {
        var normalizedStudentId = ValidateStudentId(studentId);
        return IsValidHoldId(holdId) ? repository.CancelAsync(holdId, normalizedStudentId, cancellationToken)
            : Task.FromResult(HoldTransitionOutcome.NotFound);
    }

    private static bool IsValidHoldId(string holdId) => !string.IsNullOrWhiteSpace(holdId) &&
        holdId.Length is > 2 and <= 40 && holdId.StartsWith("h_", StringComparison.Ordinal) && !holdId.Any(char.IsControl);

    private static string ValidateStudentId(string? studentId)
    {
        var normalized = studentId?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 17)
            throw new ValidationException(new ValidationResult(
                "X-Student-Id must contain 1 to 17 characters after trimming.", ["studentId"]), null, studentId);
        return normalized;
    }
}
