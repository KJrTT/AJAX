using WebApplication3.Application.Common;
using WebApplication3.Application.Common.Abstractions;
using WebApplication3.Domain;
using WebApplication3.Application.Orders.CreateOrder;


namespace WebApplication3.Application.Orders.CreateOrder;

public sealed class CreateOrderUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _uow;

    public CreateOrderUseCase(
        ICustomerRepository customers,
        IProductRepository products,
        IOrderRepository orders,
        IUnitOfWork uow)
    {
        _customers = customers;
        _products = products;
        _orders = orders;
        _uow = uow;
    }

    public async Task<Result<Guid>> ExecuteAsync(CreateOrderCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.CustomerEmail))
            return Error.Validation("email.required", "Email обязателен");

        if (cmd.Items is null || cmd.Items.Count == 0)
            return Error.Validation("items.empty", "Заказ должен содержать хотя бы один товар");

        var email = cmd.CustomerEmail.Trim().ToLowerInvariant();

        var customer = await _customers.GetByEmailAsync(email, ct);
        if (customer is null)
            return Error.NotFound("customer.not_found", "Клиент не найден");
        if (customer.IsBlocked)
            return Error.Forbidden("customer.blocked", "Клиент заблокирован");

        var items = new List<OrderItem>();
        foreach (var i in cmd.Items)
        {
            var product = await _products.GetAsync(i.ProductId, ct);
            if (product is null)
                return Error.NotFound("product.not_found", $"Товар {i.ProductId} не найден");
            if (product.Stock < i.Quantity)
                return Error.Conflict("product.out_of_stock", $"Недостаточно '{product.Name}'");

            product.DecreaseStock(i.Quantity);
            items.Add(new OrderItem(product.Id, product.Name, product.Price, i.Quantity));
        }

        var order = new Order(customer.Email, items);
        _orders.Add(order);
        await _uow.SaveChangesAsync(ct);

        return order.Id;
    }
}
