// Application/Orders/CancelOrder/CancelOrderCommand.cs
namespace WebApplication3.Application.Orders.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId);
