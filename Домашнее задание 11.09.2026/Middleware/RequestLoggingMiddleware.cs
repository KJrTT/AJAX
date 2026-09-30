using Microsoft.AspNetCore.Http;
namespace Домашнее_задание_11._09._2026.Middleware
{
    public class RequestLoggingMiddleware : IMiddleware 
    {
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("[RequestLogging] ▶ {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await next(context); 

            sw.Stop();
            _logger.LogInformation("[RequestLogging] ◀ {Path} → {Status} за {Ms} мс",
                context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
        }
    }
}
