namespace MyApi;

/// <summary>
/// Ответ для V1 API (базовый)
/// </summary>
public class ProductResponseV1
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty; // Форматированная цена "10.00 $"
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}