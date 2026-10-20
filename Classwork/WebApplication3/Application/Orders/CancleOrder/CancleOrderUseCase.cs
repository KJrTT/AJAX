using System;
using WebApplication3.Application.Common;
using WebApplication3.Application.Common.Abstractions;
using WebApplication3.Infrastructure;

namespace WebApplication3.Application.Orders.CancleOrder
{



    public sealed class CancelOrderUseCase
    {
        private readonly IOrderRepository _orders;
        private readonly AppDbContext _db;

        public CancelOrderUseCase(IOrderRepository orders, AppDbContext db)
        {
            _orders = orders; _db = db;
        }

        public async Task<Result<bool>> ExecuteAsync(CancelOrderCommand cmd, CancellationToken ct)
        {
            var order = await _orders.GetAsync(cmd.OrderId, ct);
            if (order is null) return Error.NotFound("order.not_found", "Заказ не найден");
            

            order.Cancel();

            return true;
        }
    }
}
