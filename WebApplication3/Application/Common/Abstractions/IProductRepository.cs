
using WebApplication3.Domain;

namespace WebApplication3.Application.Common.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetAsync(Guid id, CancellationToken ct);
    void Add(Product product);
}
