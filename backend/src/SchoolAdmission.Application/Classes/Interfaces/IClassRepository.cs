using SchoolAdmission.Application.Classes.DTOs;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Classes.Interfaces;

public interface IClassRepository
{
    Task AddAsync(AdmissionClass admissionClass, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClassResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<ClassResponse?> GetByIdAsync(string classId, CancellationToken cancellationToken);
}
