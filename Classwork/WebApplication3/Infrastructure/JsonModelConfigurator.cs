// WebApplication3/Infrastructure/Persistence/Configurations/Json/JsonModelConfigurator.cs
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApplication3.Infrastructure.Persistence.Configurations.Json;

namespace WebApplication3.Infrastructure.Persistence.Configurations.Json;

public static class JsonModelConfigurator
{
    public static void ApplyFromJson(ModelBuilder modelBuilder, string jsonPath, Assembly domainAssembly)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"Entity config not found: {jsonPath}");

        var json = File.ReadAllText(jsonPath);
        var root = JsonSerializer.Deserialize<EntityConfigRoot>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid entity-config.json");

        foreach (var entityCfg in root.Entities)
        {
            var clrType = domainAssembly.GetType($"WebApplication3.Domain.{entityCfg.Name}")
                          ?? domainAssembly.GetTypes().FirstOrDefault(t => t.Name == entityCfg.Name)
                          ?? throw new InvalidOperationException(
                              $"Type '{entityCfg.Name}' not found in {domainAssembly.FullName}");

            var builder = modelBuilder.Entity(clrType);

            // Таблица
            if (!string.IsNullOrWhiteSpace(entityCfg.Table))
                builder.ToTable(entityCfg.Table);

            // Свойства
            foreach (var (propName, propCfg) in entityCfg.Properties)
            {
                var prop = builder.Property(propName);
                ApplyPropertyConfig(prop, propCfg);

                if (propCfg.IsKey)
                    builder.HasKey(propName);

                if (propCfg.Unique)
                    builder.HasIndex(propName).IsUnique();
                else if (propCfg.Index)
                    builder.HasIndex(propName);
            }

            // Owned-коллекции
            foreach (var ownedCfg in entityCfg.Owned)
            {
                ApplyOwnedConfig(builder, ownedCfg);
            }
        }
    }

    // Нон-дженерик: PropertyBuilder без <T> — достаточно для IsRequired/HasMaxLength/HasColumnType
    private static void ApplyPropertyConfig(PropertyBuilder prop, PropertyConfig cfg)
    {
        if (cfg.Required) prop.IsRequired();
        if (cfg.MaxLength.HasValue) prop.HasMaxLength(cfg.MaxLength.Value);
        if (!string.IsNullOrWhiteSpace(cfg.ColumnType)) prop.HasColumnType(cfg.ColumnType);
    }

    // Нон-дженерик: builder — EntityTypeBuilder (без <T>), т.к. modelBuilder.Entity(clrType) возвращает именно его
    private static void ApplyOwnedConfig(EntityTypeBuilder builder, OwnedConfig cfg)
    {
        // Настраиваем навигацию на использование backing field
        var nav = builder.Metadata.FindNavigation(cfg.Navigation);
        if (nav is not null && !string.IsNullOrWhiteSpace(cfg.BackingField))
            nav.SetPropertyAccessMode(PropertyAccessMode.Field);

        if (nav is null)
            throw new InvalidOperationException(
                $"Navigation '{cfg.Navigation}' not found on entity '{builder.Metadata.ClrType.Name}'");

        // Тип элемента коллекции (OrderItem)
        var navigationType = nav.ClrType.GetGenericArguments().First();

        // Ищем метод OwnsMany на EntityTypeBuilder (нон-дженерик) с 2 параметрами:
        // public virtual OwnedNavigationBuilder OwnsMany(Type navigationType, Action<OwnedNavigationBuilder> buildAction)
        var ownsMany = typeof(EntityTypeBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "OwnsMany"
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(Type));

        var actionType = typeof(Action<>).MakeGenericType(
            typeof(OwnedNavigationBuilder<,>).MakeGenericType(builder.Metadata.ClrType, navigationType));

        var method = typeof(JsonModelConfigurator)
            .GetMethod(nameof(ConfigureOwnedGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(builder.Metadata.ClrType, navigationType);

        var action = Delegate.CreateDelegate(actionType, method);

        ownsMany.Invoke(builder, new object[] { navigationType, action });
    }

    private static void ConfigureOwnedGeneric<TOwner, TOwned>(
        OwnedNavigationBuilder<TOwner, TOwned> builder)
        where TOwner : class
        where TOwned : class
    {
        // Конфигурация owned-типа. Если нужны настройки из JSON —
        // передайте их сюда (например, через статическое поле или словарь).
        // Сейчас — пусто, чтобы просто зарегистрировать OwnsMany.
    }
}
