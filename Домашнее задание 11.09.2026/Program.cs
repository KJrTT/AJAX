using Домашнее_задание_11._09._2026.Middleware;
using Домашнее_задание_11._09._2026.Services;

namespace Домашнее_задание_11._09._2026
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            //КОНФИГУРАЦИЯ СЕРВИСОВ (ConfigureServices) =====
            Console.WriteLine("[1] ConfigureServices: регистрация сервисов");

            builder.Services.AddTransient<RequestLoggingMiddleware>();
            builder.Services.AddTransient<FormValidationMiddleware>();
            builder.Services.AddSingleton<AppLifetimeService>();
            builder.Services.AddHostedService<LifetimeHostedService>();

            var app = builder.Build();


            Console.WriteLine("[2] Configure: построение pipeline middleware");

            app.Use(async (context, next) =>
            {
                Console.WriteLine($"[MW-1] Incoming: {context.Request.Method} {context.Request.Path}");
                await next.Invoke();
                Console.WriteLine($"[MW-1] Outgoing: {context.Response.StatusCode}");
            });

            // Кастомный middleware логирования
            app.UseMiddleware<RequestLoggingMiddleware>();

            // Кастомный middleware валидации формы
            app.UseMiddleware<FormValidationMiddleware>();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapGet("/", async context =>
            {
                context.Response.Redirect("/form.html");
                await Task.CompletedTask;
            });

            app.MapPost("/submit", async context =>
            {
                var form = await context.Request.ReadFormAsync();
                var name = form["name"];
                var email = form["email"];
                var message = form["message"];

                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync($@"
        <html><head><meta charset='utf-8'><title>Результат</title></head>
        <body style='font-family:Arial;padding:40px'>
            <h1>✅ Форма успешно отправлена!</h1>
            <p><b>Имя:</b> {name}</p>
            <p><b>Email:</b> {email}</p>
            <p><b>Сообщение:</b> {message}</p>
            <p><a href='/form.html'>← Назад к форме</a></p>
        </body></html>");
            });

            Console.WriteLine("[3] Application.Run — приложение стартует");
            app.Run();
        }
    }
}
