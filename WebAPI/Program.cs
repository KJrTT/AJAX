using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using WebAPI.Middleware;
using WebAPI.Services;
using WebAPI.Swagger;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// ---------- Сервисы ----------
builder.Services.AddControllers();
builder.Services.AddSingleton<IBookRepository, InMemoryBookRepository>();
builder.Services.AddFeatureManagement();

// Версионирование
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true; // обратная совместимость
        options.ReportApiVersions = true;                    // api-supported-versions / api-deprecated-versions
        options.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("api-version"),
            new QueryStringApiVersionReader("api-version"));
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.MapGet("/", () => Results.Redirect("/swagger"));
// ---------- Pipeline ----------
if (app.Environment.IsDevelopment())
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (var desc in provider.ApiVersionDescriptions)
        {
            var label = desc.GroupName.ToUpperInvariant()
                      + (desc.IsDeprecated ? " (deprecated)" : "");
            options.SwaggerEndpoint($"/swagger/{desc.GroupName}/swagger.json", label);
        }
    });
}

app.UseHttpsRedirection();

// Deprecation заголовки
app.UseMiddleware<DeprecationMiddleware>();

app.MapControllers();

app.Run();