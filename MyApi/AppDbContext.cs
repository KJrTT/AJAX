using Microsoft.EntityFrameworkCore;
using MyApi;

namespace MyApi;

/// <summary>
/// Контекст базы данных для работы с продуктами
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSet — представляет таблицу в базе данных
    public DbSet<ProductEntity> Products { get; set; }

    /// <summary>
    /// Настройка моделей при создании (Fluent API)
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Настройка сущности ProductEntity
        modelBuilder.Entity<ProductEntity>(entity =>
        {
            // Первичный ключ
            entity.HasKey(e => e.Id);

            // Id — генерируется автоматически на стороне клиента (Guid.NewGuid())
            // Но можно и на стороне БД:
            entity.Property(e => e.Id)
                .HasDefaultValueSql("NEWID()");

            // Name — обязательное поле, максимум 100 символов
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            // Description — необязательное, максимум 500 символов
            entity.Property(e => e.Description)
                .HasMaxLength(500);

            // Category — обязательное, максимум 50 символов
            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(50);

            // PriceInCents — обязательное
            entity.Property(e => e.PriceInCents)
                .IsRequired();

            // StockQuantity — обязательное
            entity.Property(e => e.StockQuantity)
                .IsRequired()
                .HasDefaultValue(0);

            // IsActive — обязательное
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // CreatedAt — устанавливается автоматически при создании
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            // UpdatedAt — может быть null
            entity.Property(e => e.UpdatedAt)
                .IsRequired(false);

            // DeletedAt — может быть null
            entity.Property(e => e.DeletedAt)
                .IsRequired(false);

            // Attributes — JSON-колонка (для SQL Server)
            entity.Property(e => e.Attributes)
                .HasColumnType("nvarchar(max)")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, new System.Text.Json.JsonSerializerOptions()),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, new System.Text.Json.JsonSerializerOptions()) ?? new Dictionary<string, object>()
                );

            // Индексы для ускорения запросов
            entity.HasIndex(e => e.Category)
                .HasDatabaseName("IX_Products_Category");

            entity.HasIndex(e => e.Name)
                .HasDatabaseName("IX_Products_Name");

            entity.HasIndex(e => e.DeletedAt)
                .HasDatabaseName("IX_Products_DeletedAt");

            // Составной индекс
            entity.HasIndex(e => new { e.Category, e.DeletedAt })
                .HasDatabaseName("IX_Products_Category_DeletedAt");

            // Устанавливаем имя таблицы (опционально)
            entity.ToTable("Products");
        });

        // Можно добавить начальные данные (Seed)
        // Закомментировано, чтобы не мешать
        /*
        modelBuilder.Entity<ProductEntity>().HasData(
            new ProductEntity
            {
                Id = Guid.NewGuid(),
                Name = "Тестовый продукт",
                Description = "Описание тестового продукта",
                PriceInCents = 1000,
                Category = "Тест",
                StockQuantity = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Attributes = new Dictionary<string, object>
                {
                    { "Color", "Red" },
                    { "Size", "L" }
                }
            }
        );
        */

        base.OnModelCreating(modelBuilder);
    }
}