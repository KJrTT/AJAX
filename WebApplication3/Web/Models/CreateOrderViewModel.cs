// Models/CreateOrderViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication3.Web.Models;

public class CreateOrderViewModel
{
    [Required(ErrorMessage = "Укажите email")]
    [EmailAddress(ErrorMessage = "Некорректный email")]
    [Display(Name = "Email клиента")]
    public string CustomerEmail { get; set; } = "";

    public List<CreateOrderItemViewModel> Items { get; set; } = new();
}

public class CreateOrderItemViewModel
{
    [Required(ErrorMessage = "Укажите ProductId")]
    public Guid ProductId { get; set; }

    [Range(1, 1000, ErrorMessage = "Количество от 1 до 1000")]
    public int Quantity { get; set; } = 1;
}
