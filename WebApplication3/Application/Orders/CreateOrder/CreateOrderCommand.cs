namespace WebApplication3.Application.Orders.CreateOrder;

public sealed record CreateOrderCommand(
    string CustomerEmail,
    IReadOnlyList<CreateOrderItem> Items);

public sealed record CreateOrderItem(Guid ProductId, int Quantity);
