using System.ComponentModel.DataAnnotations;
using SchoolAdmission.Application.Classes.DTOs;
using SchoolAdmission.Application.Classes.Interfaces;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Classes;

public sealed class ClassService(IClassRepository repository)
{
    public async Task<CreateClassResponse> CreateAsync(CreateClassRequest request, CancellationToken cancellationToken)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

        var admissionClass = new AdmissionClass(
            $"c_{Guid.NewGuid():N}", request.Name!.Trim(), request.TotalSeats, DateTime.UtcNow);
        await repository.AddAsync(admissionClass, cancellationToken);

        return new CreateClassResponse(admissionClass.Id, admissionClass.Name, admissionClass.TotalSeats);
    }

    public Task<IReadOnlyList<ClassResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);

    public Task<ClassResponse?> GetByIdAsync(string classId, CancellationToken cancellationToken) =>
        repository.GetByIdAsync(classId, cancellationToken);
}
