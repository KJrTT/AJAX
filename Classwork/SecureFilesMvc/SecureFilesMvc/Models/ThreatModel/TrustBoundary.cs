namespace SecureFilesMvc.Models.ThreatModel;

/// <summary>
/// Граница доверия (Trust Boundary) — линия на диаграмме потоков данных (DFD),
/// где данные переходят из одной зоны доверия в другую.
/// На каждой такой границе необходим контроль (аутентификация, валидация, логирование).
/// </summary>
public class TrustBoundary
{
    /// <summary>Уникальный идентификатор границы (например, TB-01).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Человекочитаемое имя (например, "Браузер → Web API").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Что пересекает границу: HTTP-запрос, файл, SQL-запрос и т.п.</summary>
    public string CrossingData { get; init; } = string.Empty;

    /// <summary>Контрмеры, применяемые на этой границе.</summary>
    public IReadOnlyList<string> Controls { get; init; } = Array.Empty<string>();
}
