using SecureFilesMvc.Models.Files;

namespace SecureFilesMvc.Services;

public interface IFileStorageService
{
    Task<FileMetaDto> SaveAsync(Stream content, string originalName, string contentType, CancellationToken ct);
    Task<(Stream Stream, FileMetaDto Meta)?> OpenAsync(string id, CancellationToken ct);
}
