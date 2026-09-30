using System.Text.Json;
using MyMvcApp.Models;

namespace MyMvcApp.Services;

public class JsonTaskVersionRepository : ITaskVersionRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public JsonTaskVersionRepository(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "Data", "versions.json");
    }

    private async Task<List<TaskVersion>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return new();
        await using var stream = File.OpenRead(_filePath);
        var items = await JsonSerializer.DeserializeAsync<List<TaskVersion>>(stream, _opts, ct);
        return items ?? new();
    }

    public async Task<IReadOnlyList<TaskVersion>> GetByTaskIdAsync(int taskId, CancellationToken ct = default)
        => (await LoadAsync(ct))
            .Where(v => v.TaskItemId == taskId)
            .OrderBy(v => v.VersionNumber)
            .ToList();

    public async Task<TaskVersion?> GetByVersionAsync(int taskId, int versionNumber, CancellationToken ct = default)
        => (await LoadAsync(ct))
            .FirstOrDefault(v => v.TaskItemId == taskId && v.VersionNumber == versionNumber);
}