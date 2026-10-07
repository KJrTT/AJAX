namespace SecureFilesMvc.Services;

public record ValidationResult(bool IsValid, string? Error);

public interface IFileValidationService
{
    Task<ValidationResult> ValidateAsync(
        Stream stream, string fileName, string contentType, long size, CancellationToken ct);
}
