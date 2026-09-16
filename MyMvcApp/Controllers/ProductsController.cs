using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Services;

namespace MyMvcApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _repo;

    public ProductsController(IProductRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? category, CancellationToken ct)
    {
        var items = string.IsNullOrWhiteSpace(category)
            ? await _repo.GetAllAsync(ct)
            : await _repo.GetByCategoryAsync(category, ct);

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var product = await _repo.GetByIdAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }
}