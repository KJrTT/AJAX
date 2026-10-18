// Application/Orders/GetOrder/GetOrderUseCase.cs
using WebApplication3.Application.Common;
using WebApplication3.Domain;
using WebApplication3.Application.Common.Abstractions;
namespace WebApplication3.Application.Orders.GetOrder;

public sealed class GetOrderUseCase
{
    private readonly IOrderRepository _orders;
    public GetOrderUseCase(IOrderRepository orders) => _orders = orders;

    public async Task<Result<OrderDto>> ExecuteAsync(GetOrderQuery query, CancellationToken ct)
    {
        var order = await _orders.GetAsync(query.OrderId, ct);
        if (order is null) return Error.NotFound("order.not_found", "Заказ не найден");

        return new OrderDto(
            order.Id,
            order.CustomerEmail,
            order.Total,
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.Name, i.Price, i.Quantity)).ToList());
    }
}
