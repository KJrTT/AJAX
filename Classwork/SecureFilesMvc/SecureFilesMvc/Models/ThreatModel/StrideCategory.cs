namespace SecureFilesMvc.Models.ThreatModel;
/// <summary>
/// Категории STRIDE — методология классификации угроз, разработанная Microsoft.
/// Каждая буква обозначает класс угроз:
///   S — Spoofing (подмена)
///   T — Tampering (изменение)
///   R — Repudiation (отказ от авторства)
///   I — Information Disclosure (утечка информации)
///   D — Denial of Service (отказ в обслуживании)
///   E — Elevation of Privilege (повышение привилегий)
/// </summary>
public enum StrideCategory
{
    Spoofing,
    Tampering,
    Repudiation,
    InformationDisclosure,
    DenialOfService,
    ElevationOfPrivilege
}
