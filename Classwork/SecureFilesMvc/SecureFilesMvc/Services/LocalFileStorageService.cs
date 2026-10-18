using System.Security.Cryptography;
using System.Text.Json;
using SecureFilesMvc.Models.Files;

namespace SecureFilesMvc.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _root;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration config, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        _root = config["Storage:RootPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(_root);
    }

    public async Task<FileMetaDto> SaveAsync(Stream content, string originalName, string contentType, CancellationToken ct)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var binPath = Path.Combine(_root, id + ".bin");
        var metaPath = Path.Combine(_root, id + ".meta.json");

        // 1. Сохраняем бинарник.
        await using (var fs = File.Create(binPath))
            await content.CopyToAsync(fs, ct);

        var size = new FileInfo(binPath).Length;
        var meta = new FileMetaDto(id, originalName, size, contentType);

        // 2. Сохраняем метаданные рядом — чтобы при скачивании вернуть оригинальное имя.
        var json = JsonSerializer.Serialize(meta);
        await File.WriteAllTextAsync(metaPath, json, ct);

        _logger.LogInformation("File saved id={Id} name={Name} size={Size}", id, originalName, size);
        return meta;
    }

    public async Task<(Stream Stream, FileMetaDto Meta)?> OpenAsync(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length != 32 || !id.All(Uri.IsHexDigit))
            return null;

        var binPath = Path.Combine(_root, id + ".bin");
        var metaPath = Path.Combine(_root, id + ".meta.json");

        if (!File.Exists(binPath) || !File.Exists(metaPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(metaPath, ct);
            var meta = JsonSerializer.Deserialize<FileMetaDto>(json);
            if (meta is null) return null;

            var stream = File.OpenRead(binPath);
            return (stream, meta);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid meta.json for id={Id}", id);
            return null;
        }
    }
}
