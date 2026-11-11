using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Application.Classes;
using SchoolAdmission.Application.Classes.DTOs;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public sealed class ClassesController(ClassService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateClassResponse>> Create(CreateClassRequest request, CancellationToken cancellationToken)
    {
        var response = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { classId = response.Id }, response);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClassResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{classId}")]
    public async Task<ActionResult<ClassResponse>> GetById(string classId, CancellationToken cancellationToken)
    {
        var response = await service.GetByIdAsync(classId, cancellationToken);
        return response is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Class not found.")
            : Ok(response);
    }
}
