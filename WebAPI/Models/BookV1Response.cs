namespace WebAPI.Models;

/// <summary>
/// Контракт v1. Поле Isbn добавлено как non-breaking изменение.
/// </summary>
public class BookV1Response
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Isbn { get; set; }
}