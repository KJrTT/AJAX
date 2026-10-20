using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Reflection;
using System.Text.Json;
using WebApplication3.Infrastructure;

namespace WebApplication3.Infrastructure
{

    public static class JsonSimpleConfigurator
    {
        public static void Apply(ModelBuilder modelBuilder, string jsonPath, Assembly domainAssembly)
        {
            if (!File.Exists(jsonPath)) return;

            var root = JsonSerializer.Deserialize<EntityConfigRoot>(
                File.ReadAllText(jsonPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (root is null) return;

            foreach (var cfg in root.Entities)
            {
                var type = domainAssembly.GetTypes().FirstOrDefault(t => t.Name == cfg.Name)
                           ?? throw new InvalidOperationException($"Type {cfg.Name} not found");

                var entity = modelBuilder.Entity(type);
                if (!string.IsNullOrWhiteSpace(cfg.Table))
                    entity.ToTable(cfg.Table);

                foreach (var (propName, propCfg) in cfg.Properties)
                {
                    var prop = entity.Property(propName);
                    if (propCfg.Required) prop.IsRequired();
                    if (propCfg.MaxLength.HasValue) prop.HasMaxLength(propCfg.MaxLength.Value);
                    if (!string.IsNullOrWhiteSpace(propCfg.ColumnType)) prop.HasColumnType(propCfg.ColumnType);

                    if (propCfg.IsKey) entity.HasKey(propName);

                    if (propCfg.Unique) entity.HasIndex(propName).IsUnique();
                    else if (propCfg.Index) entity.HasIndex(propName);
                }
            }
        }
    }
}