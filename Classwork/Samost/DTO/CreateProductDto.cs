using System.ComponentModel.DataAnnotations;

namespace Samost.DTO;

public class CreateProductDto
{
    [Required(ErrorMessage = "Name обязателен")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Name должен быть от 3 до 50 символов")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Price должен быть больше 0")]
    public decimal Price { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "CategoryId должен быть больше 0")]
    public int CategoryId { get; set; }
}