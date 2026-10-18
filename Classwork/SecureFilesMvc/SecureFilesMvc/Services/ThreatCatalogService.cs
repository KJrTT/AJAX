using SecureFilesMvc.Models.ThreatModel;
using SecureFilesMvc.Services;

namespace SecureFilesMvc.Services;

public class ThreatCatalogService : IThreatCatalogService
{
    // ---------- Границы доверия ----------
    private static readonly IReadOnlyList<TrustBoundary> _boundaries = new List<TrustBoundary>
    {
        new()
        {
            Id = "TB-01",
            Name = "Браузер → Web API (HTTP)",
            CrossingData = "HTTP-запросы: JSON, multipart/form-data",
            Controls = new[]
            {
                "HTTPS + HSTS",
                "Аутентификация по API-ключу",
                "Валидация входных данных на сервере",
                "Rate limiting на уровне обратного прокси"
            }
        },
        new()
        {
            Id = "TB-02",
            Name = "Web API → Файловое хранилище",
            CrossingData = "Бинарные файлы",
            Controls = new[]
            {
                "Генерация собственного имени файла (GUID)",
                "Проверка MIME по сигнатуре",
                "Хранение вне webroot",
                "Проверка размера"
            }
        },
        new()
        {
            Id = "TB-03",
            Name = "Web API → БД (в будущем)",
            CrossingData = "SQL-запросы",
            Controls = new[] { "Параметризованные запросы", "Минимальные права пользователя БД" }
        }
    };

    // ---------- Угрозы ----------
    private static readonly IReadOnlyList<Threat> _threats = new List<Threat>
    {
        new()
        {
            Id = "TH-001",
            Category = StrideCategory.Spoofing,
            Title = "Подмена типа файла",
            Description = "Клиент заявляет Content-Type, не соответствующий содержимому.",
            TrustBoundaryId = "TB-01",
            RiskScore = 6.5,
            Mitigations = new[] { "Валидация magic bytes", "Whitelist расширений" },
            BacklogItemIds = new[] { "SB-001" }
        },
        new()
        {
            Id = "TH-002",
            Category = StrideCategory.Tampering,
            Title = "Path traversal через имя файла",
            Description = "Имя файла содержит ../ и выходит за пределы uploads.",
            TrustBoundaryId = "TB-02",
            RiskScore = 8.0,
            Mitigations = new[] { "Санитайзинг имени", "Генерация собственного id" },
            BacklogItemIds = new[] { "SB-002" }
        },
        new()
        {
            Id = "TH-003",
            Category = StrideCategory.InformationDisclosure,
            Title = "XSS через скачанный файл",
            Description = "Браузер открывает загруженный HTML/JS и выполняет его.",
            TrustBoundaryId = "TB-01",
            RiskScore = 7.0,
            Mitigations = new[] { "Content-Disposition: attachment", "X-Content-Type-Options: nosniff" },
            BacklogItemIds = new[] { "SB-003" }
        },
        new()
        {
            Id = "TH-004",
            Category = StrideCategory.ElevationOfPrivilege,
            Title = "Загрузка без аутентификации",
            Description = "Анонимный пользователь загружает файлы.",
            TrustBoundaryId = "TB-01",
            RiskScore = 9.0,
            Mitigations = new[] { "API-key authentication", "Политики ReadFiles/WriteFiles" },
            BacklogItemIds = new[] { "SB-004" }
        },
        new()
        {
            Id = "TH-005",
            Category = StrideCategory.DenialOfService,
            Title = "DoS через огромный файл",
            Description = "Файл размером больше лимита выедает память/диск.",
            TrustBoundaryId = "TB-01",
            RiskScore = 5.5,
            Mitigations = new[] { "RequestSizeLimit", "Проверка размера в валидаторе" },
            BacklogItemIds = new[] { "SB-005" }
        },
        new()
        {
            Id = "TH-006",
            Category = StrideCategory.Repudiation,
            Title = "Отсутствие трассировки",
            Description = "Невозможно связать инцидент с конкретным запросом.",
            TrustBoundaryId = "TB-01",
            RiskScore = 4.0,
            Mitigations = new[] { "X-Request-Id middleware", "Scope в логах" },
            BacklogItemIds = new[] { "SB-006" }
        }
    };

    // ---------- Security backlog ----------
    private static readonly IReadOnlyList<SecurityBacklogItem> _backlog = new List<SecurityBacklogItem>
    {
        new() { Id = "SB-001", ThreatId = "TH-001", Title = "Внедрить проверку magic bytes",
                Priority = BacklogPriority.High, Status = BacklogStatus.InProgress,
                MitigationSummary = "FileValidationService проверяет сигнатуры", Owner = "backend" },
        new() { Id = "SB-002", ThreatId = "TH-002", Title = "Покрыть тестами санитайзинг имён",
                Priority = BacklogPriority.High, Status = BacklogStatus.Open,
                MitigationSummary = "Юнит-тесты на LocalFileStorageService", Owner = "qa" },
        new() { Id = "SB-003", ThreatId = "TH-003", Title = "Проверить заголовки на всех ответах",
                Priority = BacklogPriority.Medium, Status = BacklogStatus.Done,
                MitigationSummary = "SecurityHeadersMiddleware + Content-Disposition", Owner = "backend" },
        new() { Id = "SB-004", ThreatId = "TH-004", Title = "Ротация API-ключей",
                Priority = BacklogPriority.High, Status = BacklogStatus.Open,
                MitigationSummary = "Хранить ключи в secrets, ротация раз в квартал", Owner = "devops" },
        new() { Id = "SB-005", ThreatId = "TH-005", Title = "Настроить лимиты на reverse-proxy",
                Priority = BacklogPriority.Medium, Status = BacklogStatus.InProgress,
                MitigationSummary = "nginx client_max_body_size", Owner = "devops" },
        new() { Id = "SB-006", ThreatId = "TH-006", Title = "Пробросить correlation-id в UI",
                Priority = BacklogPriority.Low, Status = BacklogStatus.Done,
                MitigationSummary = "files-client.js показывает correlationId в ошибках", Owner = "frontend" }
    };

    public IReadOnlyList<TrustBoundary> GetTrustBoundaries() => _boundaries;
    public IReadOnlyList<Threat> GetThreats() => _threats;
    public IReadOnlyList<SecurityBacklogItem> GetBacklog() => _backlog;
}
