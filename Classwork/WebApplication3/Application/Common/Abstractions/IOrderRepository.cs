using WebApplication3.Domain;


namespace WebApplication3.Application.Common.Abstractions;

public interface IOrderRepository
{
    Task<Order?> GetAsync(Guid id, CancellationToken ct);
    void Add(Order order);
}
