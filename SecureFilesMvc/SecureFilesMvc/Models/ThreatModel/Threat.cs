namespace SecureFilesMvc.Models.ThreatModel;

/// <summary>
/// Описание одной угрозы по методологии STRIDE.
/// Заполняется на этапе threat modeling, используется для построения security backlog.
/// </summary>
public class Threat
{
    /// <summary>Идентификатор угрозы (например, TH-001).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>К какой категории STRIDE относится.</summary>
    public StrideCategory Category { get; init; }

    /// <summary>Краткое название угрозы.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Описание сценария атаки.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Идентификатор границы доверия, где угроза актуальна.</summary>
    public string TrustBoundaryId { get; init; } = string.Empty;

    /// <summary>Оценка риска по CVSS-подобной шкале (0..10).</summary>
    public double RiskScore { get; init; }

    /// <summary>Рекомендуемые контрмеры.</summary>
    public IReadOnlyList<string> Mitigations { get; init; } = Array.Empty<string>();

    /// <summary>Связанные элементы security backlog.</summary>
    public IReadOnlyList<string> BacklogItemIds { get; init; } = Array.Empty<string>();
}
