namespace SecureFilesMvc.Models.Files;

/// <summary>
/// DTO метаданных файла, возвращаемый API после загрузки.
/// </summary>
public record FileMetaDto(string Id, string Name, long Size, string ContentType);
