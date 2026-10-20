using Microsoft.EntityFrameworkCore;
using WebApplication3.Application.Common.Abstractions;
using WebApplication3.Domain;
namespace WebApplication3.Infrastructure.Repositories
{


    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _db;

        public OrderRepository(AppDbContext db) => _db = db;

        public Task<Order?> GetAsync(Guid id, CancellationToken ct)
            => _db.Orders
                  .Include(o => o.Items)
                  .FirstOrDefaultAsync(o => o.Id == id, ct);

        public void Add(Order order) => _db.Orders.Add(order);
    }
}
