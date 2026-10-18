namespace SecureFilesMvc.Models.ThreatModel;
public enum BacklogStatus { Open, InProgress, Done, Accepted }

public enum BacklogPriority { Low, Medium, High, Critical }

/// <summary>
/// Элемент security backlog — конкретная задача по закрытию угрозы.
/// Из threat model формируется backlog: каждая угроза должна быть
/// либо устранена кодом/конфигом, либо явно принята (Accepted) с обоснованием.
/// </summary>
public class SecurityBacklogItem
{
    public string Id { get; init; } = string.Empty;

    /// <summary>Ссылка на угрозу, которую закрывает задача.</summary>
    public string ThreatId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;
    public BacklogPriority Priority { get; init; }
    public BacklogStatus Status { get; set; }

    /// <summary>Как именно устраняется угроза (код, конфиг, процесс).</summary>
    public string MitigationSummary { get; init; } = string.Empty;

    /// <summary>Ответственный (владелец задачи).</summary>
    public string Owner { get; init; } = string.Empty;
}
