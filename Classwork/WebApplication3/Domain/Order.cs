using WebApplication3.Domain;

namespace WebApplication3.Domain;

public class Order
{
    public Guid Id { get; private set; }
    public string CustomerEmail { get; private set; } = default!;
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public decimal Total { get; private set; }

    private readonly List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items;
    public bool IsShipped => Status == OrderStatus.Shipped;

    private Order() { }

    public Order(string customerEmail, IEnumerable<OrderItem> items)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
            throw new ArgumentException("Customer email required", nameof(customerEmail));

        var list = items.ToList();
        if (list.Count == 0)
            throw new ArgumentException("Order must contain at least one item", nameof(items));

        Id = Guid.NewGuid();
        CustomerEmail = customerEmail.Trim().ToLowerInvariant();
        Status = OrderStatus.Created;
        CreatedAtUtc = DateTime.UtcNow;
        _items = list;
        Total = list.Sum(i => i.Price * i.Quantity);
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Shipped)
            throw new InvalidOperationException("Cannot cancel shipped order");
        if (Status == OrderStatus.Cancelled)
            return;
        Status = OrderStatus.Cancelled;
    }

    public void Ship()
    {
        if (Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot ship cancelled order");
        Status = OrderStatus.Shipped;
    }
}

public enum OrderStatus
{
    Created = 0,
    Shipped = 1,
    Cancelled = 2
}
