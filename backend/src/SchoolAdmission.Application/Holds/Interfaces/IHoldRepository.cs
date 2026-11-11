namespace SchoolAdmission.Application.Holds.Interfaces;

public interface IHoldRepository
{
    Task<CreateHoldResult> CreateAsync(string classId, string studentId, CancellationToken cancellationToken);
}
