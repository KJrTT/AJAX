using MyMvcApp.Models;
using MyMvcApp.Services;
namespace MyMvcApp.Services;
public interface ITaskVersionRepository
{
    Task<IReadOnlyList<TaskVersion>> GetByTaskIdAsync(int taskId, CancellationToken ct = default);
    Task<TaskVersion?> GetByVersionAsync(int taskId, int versionNumber, CancellationToken ct = default);
}