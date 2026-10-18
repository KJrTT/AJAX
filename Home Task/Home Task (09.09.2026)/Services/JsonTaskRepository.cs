using System.Text.Json;
using MyMvcApp.Models;

namespace MyMvcApp.Services;

public class JsonTaskRepository : ITaskRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public JsonTaskRepository(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "Data", "tasks.json");
    }

    private async Task<List<TaskItem>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return new();
        await using var stream = File.OpenRead(_filePath);
        var items = await JsonSerializer.DeserializeAsync<List<TaskItem>>(stream, _opts, ct);
        return items ?? new();
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken ct = default)
        => await LoadAsync(ct);

    public async Task<TaskItem?> GetByIdAsync(int id, CancellationToken ct = default)
        => (await LoadAsync(ct)).FirstOrDefault(t => t.Id == id);

    public async Task<IReadOnlyList<TaskItem>> GetByStatusAsync(string status, CancellationToken ct = default)
        => (await LoadAsync(ct))
            .Where(t => string.Equals(t.Status, status, StringComparison.OrdinalIgnoreCase))
            .ToList();
}