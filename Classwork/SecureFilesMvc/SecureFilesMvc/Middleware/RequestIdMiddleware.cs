namespace SecureFilesMvc.Middleware;

/// <summary>
/// Проставляет X-Request-Id в каждый запрос и ответ.
/// </summary>
public class RequestIdMiddleware
{
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers["X-Request-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
            id = Guid.NewGuid().ToString("N");

        context.Items["RequestId"] = id;
        context.Response.Headers["X-Request-Id"] = id;

        using (context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Request").BeginScope(new Dictionary<string, object> { ["RequestId"] = id }))
        {
            await _next(context);
        }
    }
}
