using WebApplication3.Domain;

namespace WebApplication3.Application.Common.Abstractions
{


    public interface ICustomerRepository
    {
        Task<Customer?> GetByEmailAsync(string email, CancellationToken ct);
        void Add(Customer customer);
    }
}
