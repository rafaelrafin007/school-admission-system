using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
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
        var result = await service.CreateAsync(classId, ReadStudentId(), cancellationToken);
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

    [HttpGet("/holds/{holdId}")]
    public async Task<ActionResult<HoldDetailsResponse>> GetById(string holdId, CancellationToken cancellationToken)
    {
        var hold = await service.GetByIdAsync(holdId, cancellationToken);
        return hold is null ? Problem(statusCode: 404, title: "Hold not found.") : Ok(hold);
    }

    [HttpPost("/holds/{holdId}/confirm")]
    public async Task<ActionResult<ConfirmHoldResponse>> Confirm(string holdId, CancellationToken cancellationToken)
    {
        var outcome = await service.ConfirmAsync(holdId, ReadStudentId(), cancellationToken);
        return outcome == HoldTransitionOutcome.Succeeded
            ? Ok(new ConfirmHoldResponse(holdId, "CONFIRMED")) : TransitionProblem(outcome);
    }

    [HttpDelete("/holds/{holdId}")]
    public async Task<IActionResult> Cancel(string holdId, CancellationToken cancellationToken)
    {
        var outcome = await service.CancelAsync(holdId, ReadStudentId(), cancellationToken);
        return outcome == HoldTransitionOutcome.Succeeded ? NoContent() : TransitionProblem(outcome);
    }

    [HttpGet("/me/holds")]
    public async Task<ActionResult<IReadOnlyList<MyHoldResponse>>> GetMine(CancellationToken cancellationToken) =>
        Ok(await service.GetForStudentAsync(ReadStudentId(), cancellationToken));

    private ObjectResult TransitionProblem(HoldTransitionOutcome outcome) => outcome switch
    {
        HoldTransitionOutcome.NotFound => Problem(statusCode: 404, title: "Hold not found."),
        HoldTransitionOutcome.Forbidden => Problem(statusCode: 403, title: "This operation is not permitted."),
        HoldTransitionOutcome.InvalidState => Problem(statusCode: 409, title: "The hold cannot be changed in its current state."),
        _ => throw new InvalidOperationException("Unknown hold transition outcome.")
    };

    private string? ReadStudentId()
    {
        Request.Headers.TryGetValue("X-Student-Id", out var values);
        if (values.Count > 1)
            throw new ValidationException(new ValidationResult("Supply one X-Student-Id header value.", ["studentId"]), null, null);
        return values.FirstOrDefault();
    }
}
