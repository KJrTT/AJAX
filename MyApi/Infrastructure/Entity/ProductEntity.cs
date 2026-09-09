
namespace MyApi;
/// <summary>
/// Сущность продукта для хранения в БД
/// </summary>
public class ProductEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PriceInCents { get; set; }
    public string Category { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Dictionary<string, object> Attributes { get; set; } = new();
}