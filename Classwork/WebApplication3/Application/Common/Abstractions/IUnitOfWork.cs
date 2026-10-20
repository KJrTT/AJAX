namespace WebApplication3.Application.Common.Abstractions
{

    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken ct);
    }
}
