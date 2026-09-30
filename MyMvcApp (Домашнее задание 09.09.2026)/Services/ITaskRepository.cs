using MyMvcApp.Models;
using MyMvcApp.Services;
namespace MyMvcApp.Services;
public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken ct = default);
    Task<TaskItem?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByStatusAsync(string status, CancellationToken ct = default);
}