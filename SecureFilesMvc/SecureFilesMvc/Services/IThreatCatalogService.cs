using SecureFilesMvc.Models.ThreatModel;

namespace SecureFilesMvc.Services;

/// <summary>
/// Каталог угроз и security backlog.
/// В реальном проекте данные хранились бы в БД/файле,
/// здесь — in-memory для демонстрации.
/// </summary>
public interface IThreatCatalogService
{
    IReadOnlyList<TrustBoundary> GetTrustBoundaries();
    IReadOnlyList<Threat> GetThreats();
    IReadOnlyList<SecurityBacklogItem> GetBacklog();
}
