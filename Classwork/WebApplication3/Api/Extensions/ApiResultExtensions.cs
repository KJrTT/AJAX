// Api/Extensions/ApiResultExtensions.cs
using Microsoft.AspNetCore.Mvc;
using WebApplication3.Application.Common;

namespace WebApplication3.Api.Extensions;

public static class ApiResultExtensions
{
    public static IActionResult ToApiResult<T>(
        this Result<T> result,
        ControllerBase controller,
        Func<T, IActionResult> onSuccess)
    {
        if (result.IsSuccess)
            return onSuccess(result.Value!);

        var error = result.Error!;
        var problem = BuildProblem(error, controller.HttpContext);

        return error.Type switch
        {
            ErrorType.Validation => controller.BadRequest(problem),
            ErrorType.NotFound => controller.NotFound(problem),
            ErrorType.Conflict => controller.Conflict(problem),
            ErrorType.Forbidden => controller.StatusCode(403, problem),
            _ => controller.StatusCode(500, problem)
        };
    }

    private static ProblemDetails BuildProblem(Error error, HttpContext ctx) => new()
    {
        Title = error.Code,
        Detail = error.Message,
        Status = error.Type switch
        {
            ErrorType.Validation => 400,
            ErrorType.NotFound => 404,
            ErrorType.Conflict => 409,
            ErrorType.Forbidden => 403,
            _ => 500
        },
        Instance = ctx.Request.Path,
        Extensions = { ["errorType"] = error.Type.ToString() }
    };
}
