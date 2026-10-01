// Application/Orders/GetOrder/GetOrderQuery.cs
namespace WebApplication3.Application.Orders.GetOrder;

public sealed record GetOrderQuery(Guid OrderId);

public sealed record OrderDto(
    Guid Id,
    string CustomerEmail,
    decimal Total,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(Guid ProductId, string Name, decimal Price, int Quantity);
