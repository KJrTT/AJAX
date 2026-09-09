using System.Net;
using System.Text.Json;

namespace MyApi;

/// <summary>
/// Глобальная обработка исключений
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Произошла ошибка при обработке запроса");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = exception.Message,
            type = exception.GetType().Name,
            traceId = context.TraceIdentifier
        };

        switch (exception)
        {
            case KeyNotFoundException:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                break;
            case ArgumentException:
            case FluentValidation.ValidationException:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;
            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response = new
                {
                    error = "Внутренняя ошибка сервера",
                    type = "InternalServerError",
                    traceId = context.TraceIdentifier
                };
                break;
        }

        var json = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(json);
    }
}