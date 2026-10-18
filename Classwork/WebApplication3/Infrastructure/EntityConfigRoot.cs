// Shop.Infrastructure/Persistence/Configurations/Json/EntityConfigRoot.cs
using System.Text.Json.Serialization;

namespace WebApplication3.Infrastructure.Persistence.Configurations.Json;

public class EntityConfigRoot
{
    [JsonPropertyName("entities")]
    public List<EntityConfig> Entities { get; set; } = new();
}

public class EntityConfig
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("table")] public string Table { get; set; } = "";
    [JsonPropertyName("properties")] public Dictionary<string, PropertyConfig> Properties { get; set; } = new();
    [JsonPropertyName("owned")] public List<OwnedConfig> Owned { get; set; } = new();
}

public class PropertyConfig
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("isKey")] public bool IsKey { get; set; }
    [JsonPropertyName("required")] public bool Required { get; set; }
    [JsonPropertyName("maxLength")] public int? MaxLength { get; set; }
    [JsonPropertyName("columnType")] public string? ColumnType { get; set; }
    [JsonPropertyName("unique")] public bool Unique { get; set; }
    [JsonPropertyName("index")] public bool Index { get; set; }
}

public class OwnedConfig
{
    [JsonPropertyName("navigation")] public string Navigation { get; set; } = "";
    [JsonPropertyName("backingField")] public string BackingField { get; set; } = "";
    [JsonPropertyName("table")] public string Table { get; set; } = "";
    [JsonPropertyName("properties")] public Dictionary<string, PropertyConfig> Properties { get; set; } = new();
}
