using Microsoft.EntityFrameworkCore;

using WebApplication3.Application.Common.Abstractions;
using WebApplication3.Domain;
using WebApplication3.Infrastructure;

namespace WebApplication3.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;

    public CustomerRepository(AppDbContext db) => _db = db;

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken ct) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Email == email, ct);

    public void Add(Customer customer) => _db.Customers.Add(customer);
}
