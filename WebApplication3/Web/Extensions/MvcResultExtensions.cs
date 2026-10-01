// Web/Extensions/MvcResultExtensions.cs
using WebApplication3.Application.Common;
using Microsoft.AspNetCore.Mvc;
namespace WebApplication3.Web.Extensions;

public static class MvcResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this Result<T> result,
        Controller controller,
        Func<T, IActionResult> onSuccess)
    {
        if (result.IsSuccess) return onSuccess(result.Value!);

        var error = result.Error!;
        return error.Type switch
        {
            ErrorType.Validation => controller.BadRequest(error),
            ErrorType.NotFound => controller.NotFound(error),
            ErrorType.Conflict => controller.Conflict(error),
            ErrorType.Forbidden => controller.Forbid(),
            _ => controller.StatusCode(500, error)
        };
    }
}
