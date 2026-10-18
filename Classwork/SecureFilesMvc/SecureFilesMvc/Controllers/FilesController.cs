using Microsoft.AspNetCore.Mvc;

namespace SecureFilesMvc.Controllers;

/// <summary>
/// MVC-страница для загрузки файлов (тонкий UI поверх FilesClient).
/// </summary>
public class FilesController : Controller
{
    public IActionResult Index() => View();
}
