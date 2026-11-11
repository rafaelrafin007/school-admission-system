using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Application.Holds.DTOs;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes/{classId}/holds")]
public sealed class HoldsController(HoldService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateHoldResponse>> Create(string classId, CancellationToken cancellationToken)
    {
        Request.Headers.TryGetValue("X-Student-Id", out var studentIds);
        if (studentIds.Count > 1)
        {
            ModelState.AddModelError("studentId", "Supply one X-Student-Id header value.");
            return ValidationProblem();
        }

        var result = await service.CreateAsync(classId, studentIds.FirstOrDefault(), cancellationToken);
        return result.Outcome switch
        {
            CreateHoldOutcome.Created => StatusCode(StatusCodes.Status201Created, result.Hold),
            CreateHoldOutcome.ClassNotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Class not found."),
            CreateHoldOutcome.DuplicateApplication => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The student already has an active or confirmed application in this class."),
            CreateHoldOutcome.ClassFull => Problem(statusCode: StatusCodes.Status409Conflict, title: "No seats are available in this class."),
            _ => throw new InvalidOperationException("Unknown hold creation outcome.")
        };
    }
}
