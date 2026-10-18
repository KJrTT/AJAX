// Shop.Infrastructure/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using WebApplication3.Application.Common.Abstractions;
using WebApplication3.Domain;
using WebApplication3.Infrastructure.Persistence.Configurations.Json;
using Microsoft.AspNetCore.Hosting;

namespace WebApplication3.Infrastructure;

public class AppDbContext : DbContext, IUnitOfWork
{
    private readonly string _jsonConfigPath;

    public AppDbContext(DbContextOptions<AppDbContext> options, IWebHostEnvironment env)
    : base(options)
    {
        _jsonConfigPath = Path.Combine(env.ContentRootPath, "Persistence", "Configurations", "entity-config.json");
    }


    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        JsonModelConfigurator.ApplyFromJson(modelBuilder, _jsonConfigPath, typeof(Order).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
