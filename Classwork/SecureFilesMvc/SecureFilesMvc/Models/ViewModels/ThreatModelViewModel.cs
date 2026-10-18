using SecureFilesMvc.Models.ThreatModel;


namespace SecureFilesMvc.Models.ViewModels;

/// <summary>
/// ViewModel для страницы /Threats.
/// Собирает все три коллекции в один объект (лучше, чем ViewBag).
/// </summary>
public class ThreatModelViewModel
{
    public IReadOnlyList<TrustBoundary> Boundaries { get; init; } = Array.Empty<TrustBoundary>();
    public IReadOnlyList<Threat> Threats { get; init; } = Array.Empty<Threat>();
    public IReadOnlyList<SecurityBacklogItem> Backlog { get; init; } = Array.Empty<SecurityBacklogItem>();
}
