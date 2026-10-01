using Microsoft.EntityFrameworkCore;
using WebApplication3.Application.Common.Abstractions;
using WebApplication3.Domain;
using WebApplication3.Infrastructure;

namespace WebApplication3.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db) => _db = db;

    public Task<Product?> GetAsync(Guid id, CancellationToken ct) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public void Add(Product product) => _db.Products.Add(product);
}
