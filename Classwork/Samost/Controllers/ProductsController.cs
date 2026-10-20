using Microsoft.AspNetCore.Mvc;
using Samost.DTO;
using Samost.Services;

namespace Samost.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _service;

    public ProductsController(ProductService service)
    {
        _service = service;
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateProductDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var product = _service.Add(dto);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var product = _service.GetById(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_service.GetAll());
    }
}