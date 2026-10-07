using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using SecureFilesMvc.Middleware;
using SecureFilesMvc.Security;
using SecureFilesMvc.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// MVC
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews();

// ---------------------------------------------------------------------------
// Аутентификация по API-ключу.
// Настраиваем схему как "challenge", чтобы [Authorize] работал корректно.
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });

// ---------------------------------------------------------------------------
// Политики авторизации.
// Разделяем права на чтение и запись — принцип минимальных привилегий.
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy(Policies.ReadFiles, p => p.RequireRole("Files.Read"));
    opts.AddPolicy(Policies.WriteFiles, p => p.RequireRole("Files.Write"));
});

// ---------------------------------------------------------------------------
// Сервисы приложения.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IThreatCatalogService, ThreatCatalogService>();
builder.Services.AddSingleton<IFileValidationService, FileValidationService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

// ---------------------------------------------------------------------------
// Kestrel: глобальный лимит на размер тела запроса (TH-004).
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = FileValidationService.MaxSizeBytes;
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline middleware (порядок важен!).
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // HSTS — принудительно HTTPS на всё время max-age.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // wwwroot — публичные ассеты (js/css), но НЕ файлы загрузок.

// Собственные middleware до роутинга.
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
