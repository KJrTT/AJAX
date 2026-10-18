namespace SecureFilesMvc.Models.Files;

/// <summary>
/// Модель запроса на загрузку файла (используется во ViewModel/тестах).
/// Реальный API принимает multipart/form-data через IFormFile.
/// </summary>
public class FileUploadRequest
{
    public string? Description { get; set; }
    public bool ConfirmLicense { get; set; }
}
