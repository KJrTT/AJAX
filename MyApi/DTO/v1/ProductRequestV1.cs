namespace MyApi;

/// <summary>
/// Общий DTO для создания и обновления
/// </summary>
public class ProductRequestV1
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Stock { get; set; } = 0;
    public Dictionary<string, object> Attributes { get; set; } = new();
}