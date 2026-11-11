using System.ComponentModel.DataAnnotations;
using SchoolAdmission.Application.Holds.Interfaces;

namespace SchoolAdmission.Application.Holds;

public sealed class HoldService(IHoldRepository repository)
{
    public Task<CreateHoldResult> CreateAsync(string classId, string? studentId, CancellationToken cancellationToken)
    {
        var normalizedStudentId = studentId?.Trim();
        if (string.IsNullOrEmpty(normalizedStudentId) || normalizedStudentId.Length > 17)
        {
            throw new ValidationException(new ValidationResult(
                "X-Student-Id must contain 1 to 17 characters after trimming.", ["studentId"]), null, studentId);
        }

        if (string.IsNullOrWhiteSpace(classId) || classId.Length is <= 2 or > 40 ||
            !classId.StartsWith("c_", StringComparison.Ordinal) || classId.Any(char.IsControl))
        {
            return Task.FromResult(new CreateHoldResult(CreateHoldOutcome.ClassNotFound));
        }

        return repository.CreateAsync(classId, normalizedStudentId, cancellationToken);
    }
}
