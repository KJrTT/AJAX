namespace MyApi;

/// <summary>
/// Ответ для V2 API (расширенный)
/// </summary>
public class ProductResponseV2
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; } // Теперь decimal для удобства
    public string Currency { get; set; } = "USD";
    public string Category { get; set; } = string.Empty;
    public int Stock { get; set; }
    public bool IsActive { get; set; }
    public Dictionary<string, object> Attributes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}