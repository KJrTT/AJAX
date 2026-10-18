using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFilesMvc.Models.Files;
using SecureFilesMvc.Security;
using SecureFilesMvc.Services;

namespace SecureFilesMvc.Controllers.Api;

/// <summary>
/// REST API для работы с файлами.
/// Это ГРАНИЦА ДОВЕРИЯ (TB-01). На входе — недоверенные данные.
/// </summary>
[ApiController]
[Route("api/files")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
public class FilesApiController : ControllerBase
{
    private readonly IFileStorageService _storage;
    private readonly IFileValidationService _validator;
    private readonly ILogger<FilesApiController> _logger;

    public FilesApiController(
        IFileStorageService storage,
        IFileValidationService validator,
        ILogger<FilesApiController> logger)
    {
        _storage = storage;
        _validator = validator;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Policy = Policies.WriteFiles)]
    [RequestSizeLimit(FileValidationService.MaxSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileValidationService.MaxSizeBytes)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ApiError("no_file", "Файл не передан"));

        var claimedType = file.ContentType ?? "application/octet-stream";

        await using var stream = file.OpenReadStream();
        var validation = await _validator.ValidateAsync(stream, file.FileName, claimedType, file.Length, ct);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Upload rejected: {Reason} Name={Name}", validation.Error, file.FileName);
            return BadRequest(new ApiError("validation_failed", validation.Error!));
        }

        var stored = await _storage.SaveAsync(stream, file.FileName, claimedType, ct);

        return CreatedAtAction(nameof(Download), new { id = stored.Id }, new
        {
            id = stored.Id,
            name = stored.Name,
            size = stored.Size,
            contentType = stored.ContentType
        });
    }

    [HttpGet("{id}")]
    [Authorize(Policy = Policies.ReadFiles)]
    public async Task<IActionResult> Download(string id, CancellationToken ct)
    {
        var result = await _storage.OpenAsync(id, ct);
        if (result is null)
            return NotFound(new ApiError("not_found", "Файл не найден"));

        var (stream, meta) = result.Value;

        Response.Headers.ContentDisposition =
           $"attachment; filename*=UTF-8''{Uri.EscapeDataString(meta.Name)}";

        return File(stream, meta.ContentType, enableRangeProcessing: true);
    }
}
