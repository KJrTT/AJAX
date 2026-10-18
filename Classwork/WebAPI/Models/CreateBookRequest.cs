using System.ComponentModel.DataAnnotations;

namespace WebAPI.Models;

public class CreateBookRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Author { get; set; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal Price { get; set; }

    [MaxLength(20)]
    public string? Isbn { get; set; }
}