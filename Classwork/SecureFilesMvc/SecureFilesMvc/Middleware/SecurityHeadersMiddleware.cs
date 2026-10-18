namespace SecureFilesMvc.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_env.IsDevelopment())
        {
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self' 'unsafe-inline' data: ws: wss:; " +
                "script-src 'self' 'unsafe-inline'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "connect-src 'self' ws: wss:; " +
                "object-src 'none'";
        }
        else
        {
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self'; " +
                "img-src 'self' data:; connect-src 'self'; " +
                "object-src 'none'; base-uri 'self'";
        }

        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";

        await _next(context);
    }
}
