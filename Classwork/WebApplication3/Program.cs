using Microsoft.EntityFrameworkCore;
using WebApplication3.Application.Common.Abstractions;
using WebApplication3.Application.Orders.CreateOrder;
using WebApplication3.Application.Orders.CancleOrder;
using WebApplication3.Application.Orders.GetOrder;
using WebApplication3.Infrastructure;
using WebApplication3.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Подключение к БД SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=shop.db"));

builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.AddScoped<CreateOrderUseCase>();
builder.Services.AddScoped<GetOrderUseCase>();
builder.Services.AddScoped<CancelOrderUseCase>();

builder.Services.AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        options.ViewLocationFormats.Add("/Web/Views/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/Web/Views/Shared/{0}.cshtml");
    });
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Products.Any())
    {
        db.Products.AddRange(
            new WebApplication3.Domain.Product("Ноутбук", 79990, 10),
            new WebApplication3.Domain.Product("Мышка", 1990, 50),
            new WebApplication3.Domain.Product("Клавиатура", 4990, 20)
        );
    }

    if (!db.Customers.Any())
    {
        db.Customers.Add(new WebApplication3.Domain.Customer("test@gmail.com", "Тестовый Пользователь"));
    }

    db.SaveChanges();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

app.Run();
