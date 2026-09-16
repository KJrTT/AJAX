using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyMvcApp.ViewModels;
using MyMvcApp.Services;
using MyMvcApp.Models;
namespace MyMvcApp.Controllers;

public class HomeController : Controller
{
    private readonly IProductRepository _repo;

    public HomeController(IProductRepository repo) => _repo = repo;

    // UI-экшен: возвращает View (shell)
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Берём только категории для фильтров — не весь список товаров
        var all = await _repo.GetAllAsync(ct);
        var categories = all.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();

        var vm = new ProductListViewModel
        {
            Title = "Каталог товаров",
            ApiEndpoint = "/api/products",
            Categories = categories
        };

        return View(vm);   // Razor получает только метаданные
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
