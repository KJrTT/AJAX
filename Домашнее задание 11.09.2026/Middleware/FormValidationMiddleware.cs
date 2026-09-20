using Microsoft.AspNetCore.Http;

namespace Домашнее_задание_11._09._2026.Middleware;

public class FormValidationMiddleware : IMiddleware
{
    // пустой конструктор — тоже валиден
    public FormValidationMiddleware() { }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Method == "POST" &&
            context.Request.Path == "/submit" &&
            context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync();

            if (string.IsNullOrWhiteSpace(form["name"]))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Ошибка: поле 'name' обязательно");
                return;
            }

            var email = form["email"].ToString();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Ошибка: email некорректен");
                return;
            }
        }

        await next(context);
    }
}
