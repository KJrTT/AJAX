namespace WebAPI.Models;

/// <summary>
/// Контракт v2. Breaking change: Price теперь объект Money.
/// </summary>
public class BookV2Response
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public Money Price { get; set; } = new();
    public string? Isbn { get; set; }
}