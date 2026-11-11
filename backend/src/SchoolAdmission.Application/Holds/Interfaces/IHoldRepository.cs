using SchoolAdmission.Application.Holds.DTOs;

namespace SchoolAdmission.Application.Holds.Interfaces;

public interface IHoldRepository
{
    Task<CreateHoldResult> CreateAsync(string classId, string studentId, CancellationToken cancellationToken);
    Task<HoldDetailsResponse?> GetByIdAsync(string holdId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MyHoldResponse>> GetForStudentAsync(string studentId, CancellationToken cancellationToken);
    Task<HoldTransitionOutcome> ConfirmAsync(string holdId, string studentId, CancellationToken cancellationToken);
    Task<HoldTransitionOutcome> CancelAsync(string holdId, string studentId, CancellationToken cancellationToken);
}
