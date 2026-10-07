using System.ComponentModel.DataAnnotations;

namespace SecureFilesMvc.Services;

/// <summary>
/// Валидация загружаемых файлов (закрывает TH-005, SB-005).
///
/// ПРИНЦИП: "defense in depth". Проверяем три независимых признака:
///   1) расширение (быстро, но подделывается),
///   2) MIME-тип от клиента (тоже подделывается),
///   3) magic bytes / сигнатура содержимого (сложнее подделать).
/// Файл считается валидным, только если расширение И сигнатура входят в whitelist.
/// </summary>
public class FileValidationService : IFileValidationService
{
    public const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB

    // Whitelist разрешённых расширений. Всё, что не здесь — отказ.
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".pdf", ".txt" };

    // Сигнатуры (magic bytes) для каждого разрешённого типа.
    private static readonly Dictionary<string, byte[][]> Signatures = new()
    {
        [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".gif"] = new[] { new byte[] { 0x47, 0x49, 0x46, 0x38 } },
        [".pdf"] = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } },
        // .txt не имеет сигнатуры — валидируем отдельно (проверка на отсутствие бинарных байт)
    };

    public async Task<ValidationResult> ValidateAsync(
        Stream stream, string fileName, string contentType, long size, CancellationToken ct)
    {
        // 1. Размер — самая дешёвая проверка, делаем первой.
        if (size <= 0) return new(false, "Пустой файл");
        if (size > MaxSizeBytes) return new(false, $"Файл больше {MaxSizeBytes / 1024 / 1024} МБ");

        // 2. Расширение.
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            return new(false, $"Расширение '{ext}' не разрешено");

        // 3. Читаем первые байты для проверки сигнатуры.
        var header = new byte[8];
        var read = await stream.ReadAsync(header.AsMemory(0, 8), ct);
        stream.Position = 0; // Возвращаем поток в начало для последующей записи.

        if (read < 4) return new(false, "Файл слишком короткий");

        // 4. Проверка сигнатуры.
        var extLower = ext.ToLowerInvariant();
        if (extLower == ".txt")
        {
            // Для .txt: убеждаемся, что нет управляющих/бинарных байтов.
            if (header.Take(read).Any(b => b < 0x09 || (b > 0x0D && b < 0x20)))
                return new(false, "Содержимое не является текстом");
        }
        else if (Signatures.TryGetValue(extLower, out var sigs))
        {
            var matches = sigs.Any(sig => header.Take(sig.Length).SequenceEqual(sig));
            if (!matches) return new(false, "Сигнатура файла не соответствует расширению");
        }

        return new(true, null);
    }
}
