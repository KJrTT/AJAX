using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Services;
using MyMvcApp.ViewModels;

namespace MyMvcApp.Controllers;

[Route("api/products")]
public class ProductsController : Controller
{
    private readonly IProductRepository _repo;

    public ProductsController(IProductRepository repo) => _repo = repo;

    // GET /api/products?category=Мебель
    [HttpGet("")]
    public async Task<IActionResult> List([FromQuery] string? category, CancellationToken ct)
    {
        var items = string.IsNullOrWhiteSpace(category)
            ? await _repo.GetAllAsync(ct)
            : await _repo.GetByCategoryAsync(category, ct);

        var dtos = items.Select(ToDto);
        return Json(dtos);          // ← ключевое отличие от UI-экшена
    }

    // GET /api/products/3
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var product = await _repo.GetByIdAsync(id, ct);
        if (product is null)
            return NotFound(new { error = "Product not found", id });

        return Json(ToDto(product));
    }

    private static ProductDto ToDto(Models.Product p)
        => new(p.Id, p.Name, p.Price, p.Category);
}