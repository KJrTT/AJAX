using Microsoft.AspNetCore.Mvc;
using SecureFilesMvc.Services;

namespace SecureFilesMvc.Controllers;

/// <summary>
/// Показывает threat model и security backlog.
/// Это "живой" артефакт: код, данные и модель угроз живут в одном репозитории.
/// </summary>
public class ThreatsController : Controller
{
    private readonly IThreatCatalogService _catalog;

    public ThreatsController(IThreatCatalogService catalog) => _catalog = catalog;

    public IActionResult Index()
    {
        ViewBag.Boundaries = _catalog.GetTrustBoundaries();
        ViewBag.Threats = _catalog.GetThreats();
        ViewBag.Backlog = _catalog.GetBacklog();
        return View();
    }
}
