using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Services;
using MyMvcApp.ViewModels;
namespace MyMvcApp.Controllers;

public class HomeController : Controller
{
    private readonly ITaskRepository _repo;
    public HomeController(ITaskRepository repo) => _repo = repo;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var all = await _repo.GetAllAsync(ct);
        var categories = all.Select(t => t.Category).Distinct().OrderBy(c => c).ToList();

        var vm = new TaskListViewModel
        {
            Title = "Планировщик задач",
            ApiEndpoint = "/api/tasks",
            Categories = categories
        };
        return View(vm);
    }
}
