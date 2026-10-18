using Microsoft.Extensions.Primitives;

namespace WebAPI.Middleware;

/// <summary>
/// Добавляет RFC 8594 (Sunset) и Deprecation заголовки к ответам v1 API.
/// </summary>
public class DeprecationMiddleware
{
    private const string SunsetDate = "Sat, 31 Dec 2026 23:59:59 GMT";
    private const string SuccessorLink =
        "<https://localhost:5001/api/v2/books>; rel=\"successor-version\"";

    private readonly RequestDelegate _next;
    private readonly ILogger<DeprecationMiddleware> _logger;

    public DeprecationMiddleware(RequestDelegate next, ILogger<DeprecationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsV1Request(context.Request))
        {
            _logger.LogWarning(
                "Deprecated v1 endpoint called: {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["Deprecation"] = "true";
                headers["Sunset"] = SunsetDate;
                headers["Link"] = SuccessorLink;
                headers["Warning"] =
                    "299 - \"This API version is deprecated. Use /api/v2/books.\"";
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }

    private static bool IsV1Request(HttpRequest request)
{
    var path = request.Path.Value ?? string.Empty;

    // Явный v2 в пути → точно не v1
    if (path.Contains("/v2/", StringComparison.OrdinalIgnoreCase))
        return false;

    // Явный v1 в пути
    if (path.Contains("/v1/", StringComparison.OrdinalIgnoreCase))
        return true;

    // api-version в query
    if (request.Query.TryGetValue("api-version", out var q))
    {
        if (q == "2.0") return false;
        if (q == "1.0") return true;
    }

    // api-version в header
    if (request.Headers.TryGetValue("api-version", out var h))
    {
        if (h == "2.0") return false;
        if (h == "1.0") return true;
    }

    // Запрос без версии к /api/books → по умолчанию v1
    var isApiBooks = path.StartsWith("/api/books", StringComparison.OrdinalIgnoreCase);
    return isApiBooks;
}
}