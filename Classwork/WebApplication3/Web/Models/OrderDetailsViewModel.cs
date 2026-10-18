namespace WebApplication3.Web.Models;

public class OrderDetailsViewModel
{
    public Guid Id { get; set; }
    public string CustomerEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public bool CanBeCancelled { get; set; }
    public List<OrderItemDetailsViewModel> Items { get; set; } = new();
}

public class OrderItemDetailsViewModel
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
