// Api/Contracts/OrderContracts.cs
namespace WebApplication3.Api.Contracts;

public sealed record CreateOrderRequest(
    string CustomerEmail,
    List<CreateOrderItemRequest> Items);

public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);

public sealed record OrderResponse(
    Guid Id,
    string CustomerEmail,
    decimal Total,
    IReadOnlyList<OrderItemResponse> Items);

public sealed record OrderItemResponse(Guid ProductId, string Name, decimal Price, int Quantity);
