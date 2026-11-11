using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace SchoolAdmission.Api.Errors;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException validation)
        {
            var errors = validation.ValidationResult.MemberNames.DefaultIfEmpty(string.Empty)
                .ToDictionary(JsonNamingPolicy.CamelCase.ConvertName, _ => new[] { validation.Message });
            await Results.ValidationProblem(errors).ExecuteAsync(httpContext);
        }
        else
        {
            await Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "An unexpected server error occurred.").ExecuteAsync(httpContext);
        }

        return true;
    }
}
